using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class DoubleMusketBlock : Block
	{
		public static int Index = 425;

		public BlockMesh m_standaloneBlockMeshUnloaded;
		public BlockMesh m_standaloneBlockMeshLoaded;

		public override void Initialize()
		{
			// Usamos el modelo original de mosquete.
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

		// Estados de carga (SIMPLIFICADO: solo Empty o Loaded)
		public static bool IsLoaded(int data) => (data & 1) != 0;
		public static int SetLoaded(int data, bool loaded) => (data & ~1) | (loaded ? 1 : 0);

		// Martillo
		public static bool GetHammerState(int data) => (data & 2) != 0;
		public static int SetHammerState(int data, bool state) => (data & ~2) | ((state ? 1 : 0) << 1);

		// Tipo de bala (original)
		public static BulletBlock.BulletType? GetBulletType(int data)
		{
			int val = (data >> 4) & 15;
			if (val == 0) return null;
			return (BulletBlock.BulletType)(val - 1);
		}
		public static int SetBulletType(int data, BulletBlock.BulletType? bulletType)
		{
			int val = bulletType.HasValue ? ((int)bulletType.Value + 1) : 0;
			return (data & ~0xF0) | ((val & 15) << 4);
		}

		// Disparos restantes (0-2) -> bits 8-9
		public static int GetShotsRemaining(int data) => (data >> 8) & 3;
		public static int SetShotsRemaining(int data, int shots)
		{
			shots = Math.Clamp(shots, 0, 2);
			return (data & ~0x300) | ((shots & 3) << 8);
		}

		// Flag Anti-Tanque -> bit 10
		private const int AntiTanksBulletFlag = 1 << 10;
		public static bool IsAntiTanksBullet(int data) => (data & AntiTanksBulletFlag) != 0;
		public static int SetAntiTanksBullet(int data, bool isAntiTanks) => isAntiTanks ? (data | AntiTanksBulletFlag) : (data & ~AntiTanksBulletFlag);
	}
}
