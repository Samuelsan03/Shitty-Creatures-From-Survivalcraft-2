using System;
using Engine;
using Engine.Graphics;
using Game;

namespace Game
{
	/// <summary>
	/// Clase base abstracta para bloques de hojas que utilizan la textura personalizada "Textures/ShittyCreaturesTextures".
	/// </summary>
	public abstract class ShittyLeavesBlock : LeavesBlock
	{
		protected Texture2D m_texture;

		public override void Initialize()
		{
			m_texture = ContentManager.Get<Texture2D>("Textures/ShittyCreaturesTextures");
			base.Initialize();
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			Color color = GetLeavesBlockColor(value, generator.Terrain, x, y, z);
			generator.GenerateCubeVertices(this, value, x, y, z, color, geometry.GetGeometry(m_texture).AlphaTestSubsetsByFace);
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			color *= GetLeavesItemColor(value, environmentData);
			BlocksManager.DrawCubeBlock(primitivesRenderer, value, new Vector3(size), 1f, ref matrix, color, color, environmentData, m_texture);
		}

		public override BlockDebrisParticleSystem CreateDebrisParticleSystem(SubsystemTerrain subsystemTerrain, Vector3 position, int value, float strength)
		{
			Color color = GetLeavesBlockColor(value, subsystemTerrain.Terrain,
				Terrain.ToCell(position.X), Terrain.ToCell(position.Y), Terrain.ToCell(position.Z));
			return new BlockDebrisParticleSystem(subsystemTerrain, position, strength, DestructionDebrisScale, color,
				GetFaceTextureSlot(4, value), m_texture);
		}

		public abstract override Color GetLeavesBlockColor(int value, Terrain terrain, int x, int y, int z);
		public abstract override Color GetLeavesItemColor(int value, DrawBlockEnvironmentData environmentData);
	}
}