using Godot;
using System.Collections.Generic;

namespace Worldbreaker.UI
{
	public partial class Tile : Area3D
	{
		public TileData Data { get; private set; }

		private MeshInstance3D _meshInstance;
		private StandardMaterial3D _material;
		private Color _baseColor = new Color(0.3f, 0.6f, 0.3f);

		public void Setup(TileData data)
		{
			Data = data;
			Name = $"Tile_{data.Id}";
			BuildMesh();
		}

		private void BuildMesh()
		{
			_material = new StandardMaterial3D();
			_material.AlbedoColor = _baseColor;

			var vertices = Data.Vertices;
			var center = Data.Center;
			int n = vertices.Count;

			var positions = new System.Collections.Generic.List<Vector3>();
			var normals = new System.Collections.Generic.List<Vector3>();
			var normalDir = center.Normalized();

			for (int i = 0; i < n; i++)
			{
				positions.Add(center);
				positions.Add(vertices[i]);
				positions.Add(vertices[(i + 1) % n]);
				normals.Add(normalDir);
				normals.Add(normalDir);
				normals.Add(normalDir);
			}

			var arrays = new Godot.Collections.Array();
			arrays.Resize((int)Mesh.ArrayType.Max);
			arrays[(int)Mesh.ArrayType.Vertex] = positions.ToArray();
			arrays[(int)Mesh.ArrayType.Normal] = normals.ToArray();

			var mesh = new ArrayMesh();
			mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
			mesh.SurfaceSetMaterial(0, _material);

			_meshInstance = new MeshInstance3D();
			_meshInstance.Mesh = mesh;
			AddChild(_meshInstance);
		}
	}
}