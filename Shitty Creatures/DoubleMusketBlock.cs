using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class DoubleMusketBlock : Block
	{
		public static int Index = 541;

		public BlockMesh m_standaloneBlockMeshUnloaded;
		public BlockMesh m_standaloneBlockMeshLoaded;

		// Enum de estado de carga: vacío o cargado con bala anti-tanque
		public enum LoadState
		{
			Empty,
			AntiTanksBullet
		}

		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/ShotGun2");
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Musket", true).ParentBone);
			Matrix boneAbsoluteTransform2 = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Hammer", true).ParentBone);

			m_standaloneBlockMeshUnloaded = new BlockMesh();
			m_standaloneBlockMeshUnloaded.AppendModelMeshPart(model.FindMesh("Musket", true).MeshParts[0], boneAbsoluteTransform, false, false, false, false, Color.White);
			m_standaloneBlockMeshUnloaded.AppendModelMeshPart(model.FindMesh("Hammer", true).MeshParts[0], boneAbsoluteTransform2, false, false, false, false, Color.White);

			m_standaloneBlockMeshLoaded = new BlockMesh();
			m_standaloneBlockMeshLoaded.AppendModelMeshPart(model.FindMesh("Musket", true).MeshParts[0], boneAbsoluteTransform, false, false, false, false, Color.White);
			m_standaloneBlockMeshLoaded.AppendModelMeshPart(model.FindMesh("Hammer", true).MeshParts[0], Matrix.CreateRotationX(0.7f) * boneAbsoluteTransform2, false, false, false, false, Color.White);

			base.Initialize();
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z) { }

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			if (GetHammerState(Terrain.ExtractData(value)))
				BlocksManager.DrawMeshBlock(primitivesRenderer, m_standaloneBlockMeshLoaded, color, 2f * size, ref matrix, environmentData);
			else
				BlocksManager.DrawMeshBlock(primitivesRenderer, m_standaloneBlockMeshUnloaded, color, 2f * size, ref matrix, environmentData);
		}

		public override bool IsSwapAnimationNeeded(int oldValue, int newValue)
		{
			if (Terrain.ExtractContents(oldValue) != BlockIndex) return true;
			int oldData = Terrain.ExtractData(oldValue);
			int newData = Terrain.ExtractData(newValue);
			return SetHammerState(newData, true) != SetHammerState(oldData, true);
		}

		public override int GetDamage(int value) => (Terrain.ExtractData(value) >> 12) & 0xF;
		public override int SetDamage(int value, int damage)
		{
			int data = Terrain.ExtractData(value);
			data &= ~0xF000;
			data |= (Math.Clamp(damage, 0, 15) << 12);
			return Terrain.ReplaceData(value, data);
		}

		// Estado de carga (bit 0)
		public static LoadState GetLoadState(int data) => (LoadState)(data & 1);
		public static int SetLoadState(int data, LoadState state) => (data & ~1) | ((int)state & 1);

		// Martillo (bit 1)
		public static bool GetHammerState(int data) => (data & 2) != 0;
		public static int SetHammerState(int data, bool state) => (data & ~2) | ((state ? 1 : 0) << 1);

		// Disparos restantes (0-2) -> bits 8-9
		public static int GetShotsRemaining(int data) => (data >> 8) & 3;
		public static int SetShotsRemaining(int data, int shots)
		{
			shots = Math.Clamp(shots, 0, 2);
			return (data & ~0x300) | ((shots & 3) << 8);
		}
	}
}
