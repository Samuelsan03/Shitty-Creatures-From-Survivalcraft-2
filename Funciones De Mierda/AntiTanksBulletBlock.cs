using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class AntiTanksBulletBlock : FlatBlock
	{
		public override void Initialize()
		{
			base.Initialize();
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			// No generar vértices de terreno (igual que PoisonBulletBlock)
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			// Color RGB personalizado: 159, 88, 140
			Color customColor = new Color(159, 88, 140, 255);
			float customSize = size * 2.5f; // Tamaño más grande para destacar
			BlocksManager.DrawFlatOrImageExtrusionBlock(primitivesRenderer, value, customSize, ref matrix, null, customColor, false, environmentData);
		}

		public override BlockDebrisParticleSystem CreateDebrisParticleSystem(SubsystemTerrain subsystemTerrain, Vector3 position, int value, float strength)
		{
			// Crear partículas con el color personalizado
			Color customColor = new Color(159, 88, 140, 255);
			return new BlockDebrisParticleSystem(subsystemTerrain, position, strength, 0.1f, customColor, TextureSlot);
		}

		public override IEnumerable<int> GetCreativeValues()
		{
			yield return Terrain.MakeBlockValue(this.BlockIndex, 0, 0);
		}

		public override string GetDisplayName(SubsystemTerrain subsystemTerrain, int value)
		{
			string displayName;
			if (LanguageControl.TryGetBlock("AntiTanksBulletBlock:0", "DisplayName", out displayName))
			{
				return displayName;
			}
			return "Anti-Tanks Bullet"; // Valor por defecto en inglés
		}

		public override string GetDescription(int value)
		{
			string description;
			if (LanguageControl.TryGetBlock("AntiTanksBulletBlock:0", "Description", out description))
			{
				return description;
			}
			return "Special ammunition that will help us later. Use it wisely and only in emergencies if the musket bullet or buckshot doesn't work to apply the necessary damage.";
		}

		public override float GetProjectilePower(int value)
		{
			// Daño alto para balas antitanque - mayor que el daño base de 80 del BulletBlock original
			return 450f; // Valor personalizado para daño antitanque
		}

		public override float GetExplosionPressure(int value)
		{
			return 0f; // Sin presión de explosión
		}

		public override int GetFaceTextureSlot(int face, int value)
		{
			return TextureSlot;
		}

		// Sistema de tipos similar a BulletBlock
		public static AntiTanksBulletType GetAntiTanksBulletType(int data)
		{
			return (AntiTanksBulletType)(data & 15);
		}

		public static int SetAntiTanksBulletType(int data, AntiTanksBulletType bulletType)
		{
			return (data & -16) | (int)(bulletType & (AntiTanksBulletType)15);
		}

		// Datos para diferentes tipos (si se quisieran implementar variantes en el futuro)
		public static float[] m_sizes = new float[]
		{
			2.5f, // Tamaño base multiplicador
        };

		public static float[] m_weaponPowers = new float[]
		{
			450f, // Daño base para antitanque
        };

		public static float[] m_explosionPressures = new float[1]; // Sin explosión

		public static int Index = 329;
		public static int TextureSlot = 229; // Usando el slot 229

		public enum AntiTanksBulletType
		{
			Standard = 0,
			// Se pueden agregar más tipos aquí si es necesario
			// ArmorPiercing = 1,
			// HighExplosive = 2,
		}
	}
}
