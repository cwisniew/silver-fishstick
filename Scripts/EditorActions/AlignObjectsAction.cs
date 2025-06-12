using Godot;
using System.Collections.Generic; // For List<>
using System.Linq; // For Linq operations if needed

// Assumes EditorAction, MoveObjectAction, PlacedObject, MainScene.AlignmentMode are accessible
// Ensure correct using statements if types are in different namespaces
// e.g., using YourProject.Scripts.Core;
// e.g., using YourProject.Scripts.Nodes; // For PlacedObject

public partial class AlignObjectsAction : EditorAction
{
    private List<MoveObjectAction> individualMoveActions = new List<MoveObjectAction>();

    private struct ObjectAlignmentInfo
    {
        public PlacedObject ObjectNode;
        public Vector2[] GlobalRotatedCorners;
        public Rect2 RotatedAABB;
        public Vector2 OriginalGlobalPosition;
    }

    public AlignObjectsAction(List<PlacedObject> selectedObjects, MainScene.AlignmentMode alignMode, MainScene.AlignContext alignContext)
    {
        this.individualMoveActions = new List<MoveObjectAction>();

        if (selectedObjects == null || selectedObjects.Count < 2) return;

        List<ObjectAlignmentInfo> objectInfoList = new List<ObjectAlignmentInfo>();
        foreach (PlacedObject obj in selectedObjects)
        {
            if (obj != null && GodotObject.IsInstanceValid(obj))
            {
                Vector2[] corners = obj.GetGlobalRotatedCorners();
                if (corners == null || corners.Length != 4)
                {
                    GD.PrintWarn($"AlignObjectsAction: Could not get valid corners for {obj.Name}. Skipping.");
                    continue;
                }

                objectInfoList.Add(new ObjectAlignmentInfo
                {
                    ObjectNode = obj,
                    GlobalRotatedCorners = corners,
                    RotatedAABB = GetBoundingBoxOfCorners(corners),
                    OriginalGlobalPosition = obj.GlobalPosition
                });
            }
        }

        if (objectInfoList.Count < 2) return;

        if (alignMode == MainScene.AlignmentMode.AlignLeft ||
            alignMode == MainScene.AlignmentMode.AlignRight ||
            alignMode == MainScene.AlignmentMode.AlignHorizontalCenter)
        {
            objectInfoList = objectInfoList.OrderBy(info => info.RotatedAABB.Position.X).ToList();
        }
        else
        {
            objectInfoList = objectInfoList.OrderBy(info => info.RotatedAABB.Position.Y).ToList();
        }

        Rect2 referenceRect;
        int iterationStartIndex;

        if (alignContext == MainScene.AlignContext.ToFirstSelected)
        {
            referenceRect = objectInfoList[0].RotatedAABB;
            iterationStartIndex = 1;
        }
        else
        {
            List<Vector2> allCornersInSelection = new List<Vector2>();
            foreach (ObjectAlignmentInfo info in objectInfoList)
            {
                allCornersInSelection.AddRange(info.GlobalRotatedCorners);
            }
            if (!allCornersInSelection.Any()) {
                 GD.PrintErr("AlignObjectsAction: No valid corners found for selection bounds.");
                 return;
            }
            referenceRect = GetBoundingBoxOfCorners(allCornersInSelection);
            iterationStartIndex = 0;
        }

        for (int i = iterationStartIndex; i < objectInfoList.Count; i++)
        {
            ObjectAlignmentInfo currentObjectInfo = objectInfoList[i];
            Rect2 currentObjectAABB = currentObjectInfo.RotatedAABB;
            Vector2 oldGlobalPosition = currentObjectInfo.OriginalGlobalPosition;
            Vector2 newGlobalPosition = oldGlobalPosition;

            float deltaX = 0;
            float deltaY = 0;

            switch (alignMode)
            {
                case MainScene.AlignmentMode.AlignLeft:
                    deltaX = referenceRect.Position.X - currentObjectAABB.Position.X;
                    break;
                case MainScene.AlignmentMode.AlignRight:
                    deltaX = referenceRect.End.X - currentObjectAABB.End.X;
                    break;
                case MainScene.AlignmentMode.AlignHorizontalCenter:
                    float targetCenterX = referenceRect.Position.X + referenceRect.Size.X / 2.0f;
                    float currentCenterX = currentObjectAABB.Position.X + currentObjectAABB.Size.X / 2.0f;
                    deltaX = targetCenterX - currentCenterX;
                    break;
                case MainScene.AlignmentMode.AlignTop:
                    deltaY = referenceRect.Position.Y - currentObjectAABB.Position.Y;
                    break;
                case MainScene.AlignmentMode.AlignBottom:
                    deltaY = referenceRect.End.Y - currentObjectAABB.End.Y;
                    break;
                case MainScene.AlignmentMode.AlignVerticalCenter:
                    float targetCenterY = referenceRect.Position.Y + referenceRect.Size.Y / 2.0f;
                    float currentCenterY = currentObjectAABB.Position.Y + currentObjectAABB.Size.Y / 2.0f;
                    deltaY = targetCenterY - currentCenterY;
                    break;
            }

            newGlobalPosition = oldGlobalPosition + new Vector2(deltaX, deltaY);

            if (!oldGlobalPosition.IsEqualApprox(newGlobalPosition))
            {
                individualMoveActions.Add(new MoveObjectAction(currentObjectInfo.ObjectNode, oldGlobalPosition, newGlobalPosition));
            }
        }
    }

    public override void Execute(TileMap tileMapContext)
    {
        if (individualMoveActions.Count == 0) return;
        // GD.Print($"AlignObjectsAction Execute: Moving {individualMoveActions.Count} objects for alignment.");
        foreach (MoveObjectAction moveAction in individualMoveActions)
        {
            // MoveObjectAction.Execute doesn't use tileMapContext, it operates directly on its PlacedObject Node.
            moveAction.Execute(null); // Pass null or tileMapContext, it should be fine.
        }
    }

    public override void Undo(TileMap tileMapContext)
    {
        if (individualMoveActions.Count == 0) return;
        // GD.Print($"AlignObjectsAction Undo: Moving {individualMoveActions.Count} objects back.");
        // Undo in reverse order to maintain logical consistency if moves were dependent (though here they are not)
        for (int i = individualMoveActions.Count - 1; i >= 0; i--)
        {
            individualMoveActions[i].Undo(null); // Pass null or tileMapContext
        }
    }

    public bool IsEmpty()
    {
        return individualMoveActions.Count == 0;
    }

	public static Rect2 GetBoundingBoxOfCorners(IEnumerable<Vector2> globalCorners)
	{
		if (globalCorners == null || !globalCorners.Any())
		{
			// GD.PrintWarn("GetBoundingBoxOfCorners: Received null or empty collection of corners.");
			return new Rect2();
		}

		float minX = float.MaxValue;
		float maxX = float.MinValue;
		float minY = float.MaxValue;
		float maxY = float.MinValue;

		foreach (Vector2 corner in globalCorners)
		{
			minX = Mathf.Min(minX, corner.X);
			maxX = Mathf.Max(maxX, corner.X);
			minY = Mathf.Min(minY, corner.Y);
			maxY = Mathf.Max(maxY, corner.Y);
		}

		// Check if any valid points were processed. If not, min/max will be their initial extreme values.
        // This can happen if all points were NaN or Infinity, though .Any() should catch empty list.
        if (minX == float.MaxValue || minY == float.MaxValue || maxX == float.MinValue || maxY == float.MinValue)
        {
            // GD.PrintWarn("GetBoundingBoxOfCorners: No valid finite points found in the collection.");
            return new Rect2(); // Or handle as an error appropriately
        }

		return new Rect2(minX, minY, maxX - minX, maxY - minY);
	}
}
