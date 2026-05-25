using Godot;
using System.Collections.Generic;

namespace Worldbreaker.Systems
{
	public class Tile
	{
		public Vector3 Center { get; set; }
		public List<Vector3> Vertices { get; set; } = new();
		public List<int> NeighborIds { get; set; } = new();
		public bool IsPentagon { get; set; }
	}
}