using Godot;
using System.Collections.Generic; // For List<>
using System.Linq; // For OrderBy

// Assumes EditorAction, MoveObjectAction, PlacedObject, MainScene.DistributionMode are accessible
// Ensure correct using statements if types are in different namespaces

// Define ObjectBoundsInfo struct/class here or ensure it's accessible
// For this modification, we'll assume it's defined as in AlignObjectsAction
// struct ObjectBoundsInfo { ... }

public partial class DistributeObjectsAction : EditorAction
{
    private List<MoveObjectAction> individualMoveActions = new List<MoveObjectAction>();

    // Assuming ObjectBoundsInfo is defined somewhere accessible, like AlignObjectsAction
    // If not, it needs to be defined, e.g.:
    private struct ObjectBoundsInfo
    {
        public PlacedObject ObjectNode;
        public Vector2 GlobalCenter;
        public float ScaledHalfWidth;
        public float ScaledHalfHeight;
        public float LeftEdge => GlobalCenter.X - ScaledHalfWidth;
        public float RightEdge => GlobalCenter.X + ScaledHalfWidth;
        public float TopEdge => GlobalCenter.Y - ScaledHalfHeight;
        public float BottomEdge => GlobalCenter.Y + ScaledHalfHeight;
    }

    public DistributeObjectsAction(List<PlacedObject> selectedObjects, MainScene.DistributionMode mode)
    {
        if (selectedObjects == null) return;

        List<PlacedObject> validObjects = selectedObjects
            .Where(obj => obj != null && GodotObject.IsInstanceValid(obj) && !obj.IsQueuedForDeletion())
            .ToList();

        if (validObjects.Count < 2) // Need at least 2 objects for any distribution (min/max)
        {
            // For spacing distribution, technically 3 are needed for an intermediate object,
            // but the logic for sumOfIntermediate and numberOfGaps handles cases with 2 objects (resulting in no moves).
            return;
        }

        List<ObjectBoundsInfo> objectBoundsList = new List<ObjectBoundsInfo>();
        foreach (PlacedObject obj in validObjects)
        {
            Sprite2D sprite = obj.GetNodeOrNull<Sprite2D>("ObjectSprite");
            if (sprite == null || sprite.Texture == null)
            {
                // Fallback to object's position if no sprite/texture, treating it as point-sized
                objectBoundsList.Add(new ObjectBoundsInfo
                {
                    ObjectNode = obj,
                    GlobalCenter = obj.GlobalPosition,
                    ScaledHalfWidth = 0,
                    ScaledHalfHeight = 0
                });
            }
            else
            {
                Vector2 textureSize = sprite.Texture.GetSize();
                objectBoundsList.Add(new ObjectBoundsInfo
                {
                    ObjectNode = obj,
                    GlobalCenter = obj.GlobalPosition,
                    ScaledHalfWidth = (textureSize.X * obj.CurrentScale.X) / 2.0f,
                    ScaledHalfHeight = (textureSize.Y * obj.CurrentScale.Y) / 2.0f
                });
            }
        }

        if (objectBoundsList.Count < 2) return; // Should be caught by validObjects.Count < 2 already

        List<ObjectBoundsInfo> sortedBoundsList;
        if (mode == MainScene.DistributionMode.DistributeHorizontalCenters ||
            mode == MainScene.DistributionMode.DistributeHorizontalSpacing)
        {
            sortedBoundsList = objectBoundsList.OrderBy(b => b.GlobalCenter.X).ToList();
        }
        else // Vertical modes
        {
            sortedBoundsList = objectBoundsList.OrderBy(b => b.GlobalCenter.Y).ToList();
        }

        ObjectBoundsInfo objMinBounds = sortedBoundsList[0];
        ObjectBoundsInfo objMaxBounds = sortedBoundsList[sortedBoundsList.Count - 1];

        // If only 2 objects, they are the min/max, no intermediate objects to move.
        if (sortedBoundsList.Count < 3 &&
            (mode == MainScene.DistributionMode.DistributeHorizontalSpacing ||
             mode == MainScene.DistributionMode.DistributeVerticalSpacing))
        {
             // No intermediate objects to distribute spacing for. Min/Max don't move.
            return;
        }
        if (sortedBoundsList.Count < 3 &&
            (mode == MainScene.DistributionMode.DistributeHorizontalCenters ||
             mode == MainScene.DistributionMode.DistributeVerticalCenters))
        {
            // No intermediate objects to distribute centers for. Min/Max don't move.
            return;
        }


        if (mode == MainScene.DistributionMode.DistributeHorizontalSpacing ||
            mode == MainScene.DistributionMode.DistributeVerticalSpacing)
        {
            float sumOfIntermediateObjectSizes = 0f;
            // Collect intermediate objects' bounds for easier access if needed, or just sum their sizes
            if (sortedBoundsList.Count > 2)
            {
                for (int i = 1; i < sortedBoundsList.Count - 1; i++)
                {
                    ObjectBoundsInfo currentBounds = sortedBoundsList[i];
                    if (mode == MainScene.DistributionMode.DistributeHorizontalSpacing)
                    {
                        sumOfIntermediateObjectSizes += currentBounds.ScaledHalfWidth * 2.0f;
                    }
                    else // DistributeVerticalSpacing
                    {
                        sumOfIntermediateObjectSizes += currentBounds.ScaledHalfHeight * 2.0f;
                    }
                }
            }

            float totalAvailableSpaceForGaps;
            if (mode == MainScene.DistributionMode.DistributeHorizontalSpacing)
            {
                totalAvailableSpaceForGaps = (objMaxBounds.LeftEdge - objMinBounds.RightEdge) - sumOfIntermediateObjectSizes;
            }
            else // DistributeVerticalSpacing
            {
                totalAvailableSpaceForGaps = (objMaxBounds.TopEdge - objMinBounds.BottomEdge) - sumOfIntermediateObjectSizes;
            }

            int numberOfGaps = sortedBoundsList.Count - 1; // Total gaps between all objects (min to max)
            float gapSize = 0;
            // Avoid division by zero if only 1 object (already filtered by <2 check),
            // or if numberOfGaps is somehow <=0 (e.g. count is 1, but also filtered)
            if (numberOfGaps > 0)
            {
                 gapSize = totalAvailableSpaceForGaps / numberOfGaps;
            }

            float currentLeadingEdgeTracker;
            if (mode == MainScene.DistributionMode.DistributeHorizontalSpacing)
            {
                currentLeadingEdgeTracker = objMinBounds.RightEdge;
            }
            else // DistributeVerticalSpacing
            {
                currentLeadingEdgeTracker = objMinBounds.BottomEdge;
            }

            // Position intermediate objects
            for (int i = 1; i < sortedBoundsList.Count - 1; i++) // Loop through intermediate objects
            {
                ObjectBoundsInfo currentObjectMetaBounds = sortedBoundsList[i];
                PlacedObject objectToPosition = currentObjectMetaBounds.ObjectNode;
                Vector2 oldPosition = objectToPosition.GlobalPosition;
                Vector2 newPosition = oldPosition;

                if (mode == MainScene.DistributionMode.DistributeHorizontalSpacing)
                {
                    float targetLeftEdge = currentLeadingEdgeTracker + gapSize;
                    newPosition.X = targetLeftEdge + currentObjectMetaBounds.ScaledHalfWidth;
                    currentLeadingEdgeTracker = newPosition.X + currentObjectMetaBounds.ScaledHalfWidth; // Update: this object's right edge
                }
                else // DistributeVerticalSpacing
                {
                    float targetTopEdge = currentLeadingEdgeTracker + gapSize;
                    newPosition.Y = targetTopEdge + currentObjectMetaBounds.ScaledHalfHeight;
                    currentLeadingEdgeTracker = newPosition.Y + currentObjectMetaBounds.ScaledHalfHeight; // Update: this object's bottom edge
                }

                if (!oldPosition.IsEqualApprox(newPosition))
                {
                    individualMoveActions.Add(new MoveObjectAction(objectToPosition, oldPosition, newPosition));
                }
            }
        }
        else // Existing logic for DistributeHorizontalCenters, DistributeVerticalCenters
        {
            float totalSpan;
            if (mode == MainScene.DistributionMode.DistributeHorizontalCenters)
            {
                totalSpan = objMaxBounds.GlobalCenter.X - objMinBounds.GlobalCenter.X;
            }
            else // DistributeVerticalCenters
            {
                totalSpan = objMaxBounds.GlobalCenter.Y - objMinBounds.GlobalCenter.Y;
            }

            if (totalSpan <= 0.001f) // sortedBoundsList.Count <=2 is already handled by the check above for <3
            {
                return;
            }

            float interval = totalSpan / (sortedBoundsList.Count - 1);

            for (int i = 1; i < sortedBoundsList.Count - 1; i++)
            {
                ObjectBoundsInfo currentObjectBounds = sortedBoundsList[i];
                PlacedObject currentObjectNode = currentObjectBounds.ObjectNode;
                Vector2 oldPosition = currentObjectNode.GlobalPosition;
                Vector2 newPosition = oldPosition;

                if (mode == MainScene.DistributionMode.DistributeHorizontalCenters)
                {
                    float targetX = objMinBounds.GlobalCenter.X + (i * interval);
                    newPosition.X = targetX;
                }
                else // DistributeVerticalCenters
                {
                    float targetY = objMinBounds.GlobalCenter.Y + (i * interval);
                    newPosition.Y = targetY;
                }

                if (!oldPosition.IsEqualApprox(newPosition))
                {
                    individualMoveActions.Add(new MoveObjectAction(currentObjectNode, oldPosition, newPosition));
                }
            }
        }
    }

    public override void Execute(TileMap tileMapContext)
    {
        if (IsEmpty()) return;
        // GD.Print($"DistributeObjectsAction Execute: Moving {individualMoveActions.Count} objects for distribution.");
        foreach (MoveObjectAction moveAction in individualMoveActions)
        {
            // MoveObjectAction.Execute doesn't use tileMapContext
            moveAction.Execute(null);
        }
    }

    public override void Undo(TileMap tileMapContext)
    {
        if (IsEmpty()) return;
        // GD.Print($"DistributeObjectsAction Undo: Moving {individualMoveActions.Count} objects back.");
        for (int i = individualMoveActions.Count - 1; i >= 0; i--)
        {
            individualMoveActions[i].Undo(null);
        }
    }

    public bool IsEmpty()
    {
        return individualMoveActions.Count == 0;
    }
}
