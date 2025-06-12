using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public static class Pathfinder
{
	private class PathNode : IComparable<PathNode>
	{
		public Vector2I GridPosition { get; }
		public Vector2 WorldPosition { get; } // Store world position for easy path reconstruction
		public float GCost { get; set; } // Cost from start to this node
		public float HCost { get; set; } // Heuristic cost from this node to end
		public float FCost => GCost + HCost;
		public PathNode ParentNode { get; set; }

		public PathNode(Vector2I gridPosition, Vector2 worldPosition)
		{
			GridPosition = gridPosition;
			WorldPosition = worldPosition;
		}

		public int CompareTo(PathNode other) // For Min-Priority Queue behavior if using SortedSet/List.Sort
		{
			int compare = FCost.CompareTo(other.FCost);
			if (compare == 0)
			{
				compare = HCost.CompareTo(other.HCost); // Tie-breaker
			}
			return compare;
		}
	}

	public static List<Vector2> FindPath(Vector2 startWorld, Vector2 endWorld,
										 Godot.Collections.Array<StaticBody2D> obstacles,
										 Rect2 mapBounds, float gridCellSize,
										 PhysicsDirectSpaceState2D spaceState) // Added spaceState
	{
		if (gridCellSize <= 0) return null;

		// 1. Grid Creation
		int gridWidth = Mathf.FloorToInt(mapBounds.Size.X / gridCellSize);
		int gridHeight = Mathf.FloorToInt(mapBounds.Size.Y / gridCellSize);
		PathNode[,] grid = new PathNode[gridWidth, gridHeight];
		bool[,] isWalkableGrid = new bool[gridWidth, gridHeight];

		PhysicsShapeQueryParameters2D queryParams = new PhysicsShapeQueryParameters2D();
		RectangleShape2D cellShape = new RectangleShape2D();
		cellShape.Size = new Vector2(gridCellSize, gridCellSize) * 0.9f; // Check slightly smaller cell to avoid edge cases

		for (int x = 0; x < gridWidth; x++)
		{
			for (int y = 0; y < gridHeight; y++)
			{
				Vector2 cellWorldCenter = mapBounds.Position + new Vector2(x * gridCellSize + gridCellSize / 2, y * gridCellSize + gridCellSize / 2);
				grid[x, y] = new PathNode(new Vector2I(x, y), cellWorldCenter);
				isWalkableGrid[x,y] = true; // Assume walkable first

				// Obstacle check using IntersectPoint or IntersectShape centered on cell
				// This is more accurate than just AABB of obstacles.
				queryParams.Transform = new Transform2D(0, cellWorldCenter);
				queryParams.Shape = cellShape;
				// queryParams.CollisionMask = ???; // Mask for obstacles (e.g. layer 2) - needs to be set if obstacles are on specific layers
				// For now, assume obstacles passed are the ones to check against, ignore their layer property.
				// If obstacles have complex collision shapes, this check might need to be more robust
				// or use the simpler AABB check initially planned.
				// Let's use IntersectShape:
				var intersections = spaceState.IntersectShape(queryParams);
				if (intersections.Count > 0)
				{
					// Check if any intersection is with one of the provided obstacles
					// This is important if other physics bodies exist that aren't "obstacles" for pathfinding
					foreach(var intersection in intersections)
					{
						if (intersection["collider"].AsGodotObject() is StaticBody2D body && obstacles.Contains(body))
						{
							isWalkableGrid[x,y] = false;
							break;
						}
					}
				}
			}
		}

		// Helper to convert world to grid - ensure it clamps within bounds
		Func<Vector2, Vector2I> worldToGrid = (Vector2 worldPos) =>
		{
			int gx = Mathf.Clamp(Mathf.FloorToInt((worldPos.X - mapBounds.Position.X) / gridCellSize), 0, gridWidth - 1);
			int gy = Mathf.Clamp(Mathf.FloorToInt((worldPos.Y - mapBounds.Position.Y) / gridCellSize), 0, gridHeight - 1);
			return new Vector2I(gx, gy);
		};

		Vector2I startGridPos = worldToGrid(startWorld);
		Vector2I endGridPos = worldToGrid(endWorld);

		PathNode startNode = grid[startGridPos.X, startGridPos.Y];
		PathNode endNode = grid[endGridPos.X, endGridPos.Y];

		if (!isWalkableGrid[startGridPos.X, startGridPos.Y] || !isWalkableGrid[endGridPos.X, endGridPos.Y])
		{
			GD.Print("Start or End node is not walkable.");
			return null; // Start or end is unwalkable
		}

		// 2. A* Algorithm
		List<PathNode> openList = new List<PathNode>();
		HashSet<Vector2I> closedList = new HashSet<Vector2I>();
		openList.Add(startNode);

		int[] dx = { -1, 1, 0, 0, -1, -1, 1, 1 }; // For 8-directional movement
		int[] dy = { 0, 0, -1, 1, -1, 1, -1, 1 };
		float[] moveCost = { 1, 1, 1, 1, 1.414f, 1.414f, 1.414f, 1.414f }; // Cost for straight and diagonal

		while (openList.Count > 0)
		{
			openList.Sort(); // Sort by FCost (PathNode implements IComparable)
			PathNode currentNode = openList[0];
			openList.RemoveAt(0);
			closedList.Add(currentNode.GridPosition);

			if (currentNode == endNode)
			{
				return ReconstructPath(endNode);
			}

			for (int i = 0; i < 8; i++) // Check 8 neighbors
			{
				Vector2I neighborGridPos = new Vector2I(currentNode.GridPosition.X + dx[i], currentNode.GridPosition.Y + dy[i]);

				if (neighborGridPos.X < 0 || neighborGridPos.X >= gridWidth ||
					neighborGridPos.Y < 0 || neighborGridPos.Y >= gridHeight ||
					!isWalkableGrid[neighborGridPos.X, neighborGridPos.Y] ||
					closedList.Contains(neighborGridPos))
				{
					continue;
				}

				// Diagonal movement check: ensure direct path is clear (no corner cutting)
				if (dx[i] != 0 && dy[i] != 0) // If it's a diagonal neighbor
				{
					if (!isWalkableGrid[currentNode.GridPosition.X + dx[i], currentNode.GridPosition.Y] || // Check horizontal adjacent
						!isWalkableGrid[currentNode.GridPosition.X, currentNode.GridPosition.Y + dy[i]])   // Check vertical adjacent
					{
						continue; // Blocked diagonal
					}
				}


				PathNode neighborNode = grid[neighborGridPos.X, neighborGridPos.Y];
				float tentativeGCost = currentNode.GCost + moveCost[i]; // Use moveCost for diagonal/straight

				bool isInOpenList = openList.Contains(neighborNode);
				if (tentativeGCost < neighborNode.GCost || !isInOpenList)
				{
					neighborNode.GCost = tentativeGCost;
					neighborNode.HCost = CalculateHeuristic(neighborGridPos, endGridPos);
					neighborNode.ParentNode = currentNode;

					if (!isInOpenList)
					{
						openList.Add(neighborNode);
					}
				}
			}
		}
		GD.Print("Pathfinder: Open list empty, no path found.");
		return null; // No path found
	}

	private static float CalculateHeuristic(Vector2I a, Vector2I b)
	{
		// Manhattan distance for grid (can also use Euclidean or Diagonal)
		// return Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);
		// Diagonal distance (Chebyshev distance for d1=1, d2=1, or Octile for d1=1, d2=sqrt(2))
		int dx = Mathf.Abs(a.X - b.X);
		int dy = Mathf.Abs(a.Y - b.Y);
		return (dx + dy) + (1.414f - 2) * Mathf.Min(dx, dy); // Octile distance for cost 1 and Sqrt(2)
	}

	private static List<Vector2> ReconstructPath(PathNode endNode)
	{
		List<Vector2> path = new List<Vector2>();
		PathNode currentNode = endNode;
		while (currentNode != null)
		{
			path.Add(currentNode.WorldPosition); // Add world position
			currentNode = currentNode.ParentNode;
		}
		path.Reverse(); // Reverse to get path from start to end
		return path;
	}
}
