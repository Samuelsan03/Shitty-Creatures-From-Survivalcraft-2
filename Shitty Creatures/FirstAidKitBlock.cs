using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;

namespace Game
{
	public class FirstAidKitBlock : ShittyTexturesFlat
	{
		public static int Index = 520;

		private Texture2D m_largeTexture;
		private Texture2D m_mediumTexture;

		public FirstAidKitBlock() : base(null)
		{
			this.BlockIndex = 466;
		}

		public enum FirstAidKitType
		{
			Large,
			Medium
		}

		public static FirstAidKitType GetFirstAidKitType(int data)
		{
			return (FirstAidKitType)(data & 15);
		}

		public static int SetFirstAidKitType(int data, FirstAidKitType type)
		{
			return (data & -16) | (int)type;
		}

		public override void Initialize()
		{
			base.Initialize();
			// Cargamos ambas texturas manualmente ya que la base solo soporta una
			m_largeTexture = ContentManager.Get<Texture2D>("Textures/Items/botiquin grande", null);
			m_mediumTexture = ContentManager.Get<Texture2D>("Textures/Items/botiquin mediano", null);
		}

		public override Texture2D GetTextureForValue(int value)
		{
			FirstAidKitType type = GetFirstAidKitType(Terrain.ExtractData(value));
			if (type == FirstAidKitType.Large)
			{
				return m_largeTexture;
			}
			return m_mediumTexture;
		}

		public override string GetDisplayName(SubsystemTerrain subsystemTerrain, int value)
		{
			int type = (int)GetFirstAidKitType(Terrain.ExtractData(value));

			// ArrowBlock style: Obtiene el nombre del array usando el índice (0 o 1)
			return LanguageControl.Get("FirstAidKitBlock", type);
		}

		public override IEnumerable<int> GetCreativeValues()
		{
			yield return Terrain.MakeBlockValue(BlockIndex, 0, (int)FirstAidKitType.Large);
			yield return Terrain.MakeBlockValue(BlockIndex, 0, (int)FirstAidKitType.Medium);
		}
	}
}
