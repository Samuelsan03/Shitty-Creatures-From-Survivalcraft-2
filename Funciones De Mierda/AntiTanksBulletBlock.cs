using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class AntiTanksBulletBlock : BulletBlock
	{
		public AntiTanksBulletBlock() : base()
		{
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			// Color RGB personalizado: 159, 88, 140
			Color customColor = new Color(159, 88, 140);
			float customSize = size * 0.35f; // 3.5px relativo
			BlocksManager.DrawFlatOrImageExtrusionBlock(primitivesRenderer, value, customSize, ref matrix, null, customColor, false, environmentData);
		}

		public override string GetDisplayName(SubsystemTerrain subsystemTerrain, int value)
		{
			string displayName = LanguageControl.GetBlock("AntiTanksBulletBlock", "DisplayName");
			return string.IsNullOrEmpty(displayName) ? "Anti-Tanks Bullet" : displayName;
		}

		public override string GetDescription(int value)
		{
			string description = LanguageControl.GetBlock("AntiTanksBulletBlock", "Description");
			return string.IsNullOrEmpty(description) ? "Special ammunition that will help us later. Use it wisely and only in emergencies if the musket bullet or buckshot doesn't work to apply the necessary damage." : description;
		}

		public static int Index = 329;
	}
}
