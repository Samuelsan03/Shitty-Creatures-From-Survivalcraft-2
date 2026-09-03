using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class CandyBlock : ShittyTexturesFlat
	{
		private static Texture2D[] s_textures;

		public CandyBlock() : base("Textures/Items/caramelo 1")
		{
			this.BlockIndex = 527;
			DefaultSicknessProbability = 0f;
		}

		public override void Initialize()
		{
			base.Initialize();
			if (s_textures == null)
			{
				s_textures = new Texture2D[m_texturePaths.Length];
				for (int i = 0; i < m_texturePaths.Length; i++)
				{
					s_textures[i] = ContentManager.Get<Texture2D>(m_texturePaths[i]);
				}
			}
		}

		public override Texture2D GetTextureForValue(int value)
		{
			CandyBlock.CandyType candyType = CandyBlock.GetCandyType(Terrain.ExtractData(value));
			int index = (int)candyType;
			if (s_textures != null && index >= 0 && index < s_textures.Length)
			{
				return s_textures[index];
			}
			return s_textures != null ? s_textures[0] : m_texture;
		}

		// ✅ MODIFICADO: Ahora usa LanguageControl como ArrowBlock
		public override string GetDisplayName(SubsystemTerrain subsystemTerrain, int value)
		{
			int candyType = (int)CandyBlock.GetCandyType(Terrain.ExtractData(value));
			if (candyType < 0 || candyType >= Enum.GetValues<CandyBlock.CandyType>().Length)
			{
				return string.Empty;
			}
			return LanguageControl.Get("CandyBlock", candyType);
		}

		public override IEnumerable<int> GetCreativeValues()
		{
			for (int i = 0; i < CandyBlock.m_order.Length; i++)
			{
				yield return Terrain.MakeBlockValue(BlockIndex, 0, CandyBlock.SetCandyType(0, (CandyBlock.CandyType)CandyBlock.m_order[i]));
			}
		}

		public override float GetNutritionalValue(int value)
		{
			return 0f;
		}

		public override float GetIconViewScale(int value, DrawBlockEnvironmentData environmentData)
		{
			int candyType = (int)CandyBlock.GetCandyType(Terrain.ExtractData(value));
			if (candyType < 0 || candyType >= CandyBlock.m_iconViewScales.Length)
			{
				return 1f;
			}
			return CandyBlock.m_iconViewScales[candyType];
		}

		public static CandyBlock.CandyType GetCandyType(int data)
		{
			return (CandyBlock.CandyType)(data & 15);
		}

		public static int SetCandyType(int data, CandyBlock.CandyType candyType)
		{
			return (data & -16) | (int)(candyType & (CandyBlock.CandyType)15);
		}

		public static new int Index = 527;

		public static int[] m_order = new int[]
		{
			0,  // FireCandy
            1,  // PoisonCandy
            2,  // FrozenCandy
            3   // BloodCandy
        };

		// ❌ ELIMINADO - Ya no necesitas este array
		// public static string[] m_displayNames = new string[] { ... };

		public static float[] m_iconViewScales = new float[]
		{
			0.85f,
			0.85f,
			0.85f,
			0.85f
		};

		private static string[] m_texturePaths = new string[]
		{
			"Textures/Items/caramelo 1",  // FireCandy
            "Textures/Items/caramelo 2",  // PoisonCandy
            "Textures/Items/caramelo 3",  // FrozenCandy
            "Textures/Items/caramelo 4"   // BloodCandy
        };

		public enum CandyType
		{
			FireCandy,    // 0
			PoisonCandy,  // 1
			FrozenCandy,  // 2
			BloodCandy    // 3
		}
	}
}