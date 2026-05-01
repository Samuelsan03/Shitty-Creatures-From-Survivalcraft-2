using System;
using System.Collections.Generic;
using System.Linq;
using Engine;
using Engine.Graphics;
using Game;

namespace Game
{
	// Token: 0x0200000C RID: 12
	public class AirConditionerBlock : Block
	{
		// Token: 0x0600003E RID: 62 RVA: 0x00002CF8 File Offset: 0x00000EF8
		public override void Initialize()
		{
			this.m_texture1 = ContentManager.Get<Texture2D>("Textures/AirConditioner");
			Model model = ContentManager.Get<Model>("Models/AirConditioner");
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Body", true).ParentBone);
			this.m_blockMeshesBySize[0] = new BlockMesh();
			this.m_blockMeshesBySize[0].AppendModelMeshPart(model.FindMesh("Body", true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(0.5f, 0f, 0.5f), false, false, false, false, Color.White);
			this.m_blockMeshesBySize[1] = new BlockMesh();
			this.m_blockMeshesBySize[1].AppendModelMeshPart(model.FindMesh("Body", true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(-0.5f, 0f, 0.5f) * Matrix.CreateRotationY(1.5707964f), false, false, false, false, Color.White);
			this.m_blockMeshesBySize[2] = new BlockMesh();
			this.m_blockMeshesBySize[2].AppendModelMeshPart(model.FindMesh("Body", true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(-0.5f, 0f, -0.5f) * Matrix.CreateRotationY(3.1415927f), false, false, false, false, Color.White);
			this.m_blockMeshesBySize[3] = new BlockMesh();
			this.m_blockMeshesBySize[3].AppendModelMeshPart(model.FindMesh("Body", true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(0.5f, 0f, -0.5f) * Matrix.CreateRotationY(4.712389f), false, false, false, false, Color.White);
			this.m_standaloneBlockMeshesBySize = new BlockMesh();
			this.m_standaloneBlockMeshesBySize.AppendModelMeshPart(model.FindMesh("Body", true).MeshParts[0], boneAbsoluteTransform, false, false, false, false, Color.White);
			this.m_collisionBoxesBySize[0] = new BoundingBox[]
			{
				new BoundingBox(new Vector3(0f, 0f, 0.1f), new Vector3(1f, 1f, 0.9f))
			};
			this.m_collisionBoxesBySize[1] = new BoundingBox[]
			{
				new BoundingBox(new Vector3(0.1f, 0f, 0f), new Vector3(0.9f, 1f, 1f))
			};
			base.Initialize();
		}

		// Token: 0x0600003F RID: 63 RVA: 0x00002F84 File Offset: 0x00001184
		public override int GetFaceTextureSlot(int face, int value)
		{
			return 0;
		}

		// Token: 0x06000040 RID: 64 RVA: 0x00002F87 File Offset: 0x00001187
		public override int GetTextureSlotCount(int value)
		{
			return 1;
		}

		// Token: 0x06000041 RID: 65 RVA: 0x00002F8C File Offset: 0x0000118C
		public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
		{
			int direction = AirConditionerBlock.GetDirection(Terrain.ExtractData(value));
			BoundingBox[] result;
			if (direction == 0 || direction == 2)
			{
				result = this.m_collisionBoxesBySize[0];
			}
			else
			{
				result = this.m_collisionBoxesBySize[1];
			}
			return result;
		}

		// Token: 0x06000042 RID: 66 RVA: 0x00002FC1 File Offset: 0x000011C1
		public override int GetEmittedLightAmount(int value)
		{
			return Math.Clamp(AirConditionerBlock.GetTemperatureRange(Terrain.ExtractData(value)), 0, 15);
		}

		// Token: 0x06000043 RID: 67 RVA: 0x00002FD8 File Offset: 0x000011D8
		public override BlockPlacementData GetPlacementValue(SubsystemTerrain subsystemTerrain, ComponentMiner componentMiner, int value, TerrainRaycastResult raycastResult)
		{
			Vector3 forward = Matrix.CreateFromQuaternion(componentMiner.ComponentCreature.ComponentCreatureModel.EyeRotation).Forward;
			float[] array = new float[]
			{
				Vector3.Dot(forward, Vector3.UnitZ),
				Vector3.Dot(forward, Vector3.UnitX),
				Vector3.Dot(forward, -Vector3.UnitZ),
				Vector3.Dot(forward, -Vector3.UnitX)
			};
			int direction = Array.IndexOf<float>(array, array.Max());
			BlockPlacementData result = default(BlockPlacementData);
			int data = Terrain.ExtractData(value);
			result.Value = Terrain.MakeBlockValue(AirConditionerBlock.Index, 0, AirConditionerBlock.SetDirection(data, direction));
			result.CellFace = raycastResult.CellFace;
			return result;
		}

		// Token: 0x06000044 RID: 68 RVA: 0x00003090 File Offset: 0x00001290
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			int direction = AirConditionerBlock.GetDirection(Terrain.ExtractData(value));
			generator.GenerateMeshVertices(this, x, y, z, this.m_blockMeshesBySize[direction], Color.White, null, geometry.GetGeometry(this.m_texture1).SubsetOpaque);
		}

		// Token: 0x06000045 RID: 69 RVA: 0x000030DD File Offset: 0x000012DD
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMeshesBySize, this.m_texture1, color, size, ref matrix, environmentData);
		}

		// Token: 0x06000046 RID: 70 RVA: 0x000030F8 File Offset: 0x000012F8
		public override void GetDropValues(SubsystemTerrain subsystemTerrain, int oldValue, int newValue, int toolLevel, List<BlockDropValue> dropValues, out bool showDebris)
		{
			showDebris = true;
			if (toolLevel < this.RequiredToolLevel)
			{
				return;
			}
			int data = Terrain.ExtractData(oldValue);
			dropValues.Add(new BlockDropValue
			{
				Value = Terrain.MakeBlockValue(AirConditionerBlock.Index, 0, data),
				Count = 1
			});
		}

		// Token: 0x06000047 RID: 71 RVA: 0x00003145 File Offset: 0x00001345
		public override BlockDebrisParticleSystem CreateDebrisParticleSystem(SubsystemTerrain subsystemTerrain, Vector3 position, int value, float strength)
		{
			return new BlockDebrisParticleSystem(subsystemTerrain, position, strength, this.DestructionDebrisScale, Color.White, 0, this.m_texture1);
		}

		// Token: 0x06000048 RID: 72 RVA: 0x00003162 File Offset: 0x00001362
		public static int GetDirection(int data)
		{
			return data & 3;
		}

		// Token: 0x06000049 RID: 73 RVA: 0x00003167 File Offset: 0x00001367
		public static int SetDirection(int data, int direction)
		{
			return (data & -4) | (direction & 3);
		}

		// Token: 0x0600004A RID: 74 RVA: 0x00003171 File Offset: 0x00001371
		public static int GetTemperatureRange(int data)
		{
			data >>= 2;
			return data & 15;
		}

		// Token: 0x0600004B RID: 75 RVA: 0x0000317C File Offset: 0x0000137C
		public static int SetTemperatureRange(int data, int range)
		{
			return (data & -61) | (range & 15) << 2;
		}

		// Token: 0x04000015 RID: 21
		public static int Index = 809;

		// Token: 0x04000016 RID: 22
		public BlockMesh m_standaloneBlockMeshesBySize;

		// Token: 0x04000017 RID: 23
		public BlockMesh[] m_blockMeshesBySize = new BlockMesh[4];

		// Token: 0x04000018 RID: 24
		public BoundingBox[][] m_collisionBoxesBySize = new BoundingBox[2][];

		// Token: 0x04000019 RID: 25
		public Texture2D m_texture1;
	}
}
