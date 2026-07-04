using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using TemplatesDatabase;
using Game;

namespace Game
{
	public class InfectedSpawnEggBlock : Block
	{
		public static int Index = 644;

		public enum InfectedType
		{
			Common,
			Boomer,
			Special,
			Flying,
			Animal
		}

		private static readonly Dictionary<InfectedType, float> s_scales = new Dictionary<InfectedType, float>
		{
			{ InfectedType.Common, 2f },
			{ InfectedType.Boomer, 2.4f },
			{ InfectedType.Special, 3.6f },
			{ InfectedType.Flying, 1.2f },
			{ InfectedType.Animal, 2f }
		};

		private static readonly Dictionary<InfectedType, Color> s_colors = new Dictionary<InfectedType, Color>
		{
			{ InfectedType.Common, new Color(0, 200, 0) },
			{ InfectedType.Boomer, new Color(255, 165, 0) },
			{ InfectedType.Special, new Color(128, 0, 128) },
			{ InfectedType.Flying, new Color(135, 206, 235) },
			{ InfectedType.Animal, new Color(255, 0, 0) }
		};

		private BlockMesh m_blockMesh;
		private Texture2D m_alertTexture;

		public InfectedSpawnEggBlock()
		{
			DefaultCategory = "Spawner Eggs";
			StaticBlockIndex = true;    // <--- indica que el índice es fijo
		}

		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/Egg");
			Matrix boneTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Egg", true).ParentBone);

			// ✅ Cambiado a false para no crashear si no existe
			m_alertTexture = ContentManager.Get<Texture2D>("Textures/alerta", throwOnNotFound: false);

			m_blockMesh = new BlockMesh();
			m_blockMesh.AppendModelMeshPart(
				model.FindMesh("Egg", true).MeshParts[0],
				boneTransform,
				false, false, false, false,
				Color.White
			);
			base.Initialize();
		}

		public static int GetValue(InfectedType type)
		{
			return Terrain.MakeBlockValue(Index, 0, Convert.ToInt32(type));
		}

		public static InfectedType GetInfectedType(int value)
		{
			int typeData = Terrain.ExtractData(value);
			if (Enum.IsDefined(typeof(InfectedType), typeData))
			{
				return (InfectedType)typeData;
			}
			return InfectedType.Common;
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z) { }

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			InfectedType type = GetInfectedType(value);
			float scale = s_scales[type];
			Color typeColor = s_colors[type];

			// ✅ FORMA LITERAL CORRECTA EN SURVIVAL CRAFT
			// Se extrae el Vector3 (R, G, B) del color del tipo, se multiplica por la intensidad del color del entorno (que actúa como luz/alpha)
			Color finalColor = new Color((byte)((float)typeColor.R * color.R / 255f), (byte)((float)typeColor.G * color.G / 255f), (byte)((float)typeColor.B * color.B / 255f));

			float scaledSize = size * scale;

			if (m_alertTexture != null)
			{
				// ✅ TU TEXTURA CONSERVADA
				BlocksManager.DrawMeshBlock(primitivesRenderer, m_blockMesh, m_alertTexture, finalColor, scaledSize, ref matrix, environmentData);
			}
			else
			{
				BlocksManager.DrawMeshBlock(primitivesRenderer, m_blockMesh, finalColor, scaledSize, ref matrix, environmentData);
			}
		}

		public override IEnumerable<int> GetCreativeValues()
		{
			foreach (InfectedType type in Enum.GetValues(typeof(InfectedType)))
			{
				yield return GetValue(type);
			}
		}
	}
}
