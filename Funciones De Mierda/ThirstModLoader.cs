using System;
using Engine;
using Game;

public class ThirstModLoader : ModLoader
{
	public override void GuiUpdate(ComponentGui componentGui)
	{
		var container = componentGui.ControlsContainerWidget.Children.Find<CanvasWidget>("WaterBarContainer", false);
		if (container != null)
		{
			var waterBar = container.Children.Find<ValueBarWidget>("WaterBar", false);
			if (waterBar != null)
			{
				var thirst = componentGui.m_componentPlayer.Entity.FindComponent<ComponentThirst>();
				if (thirst != null)
				{
					waterBar.Value = thirst.Water;
				}
			}
		}
	}

	public override bool OnPlayerSpawned(PlayerData.SpawnMode spawnMode, ComponentPlayer componentPlayer, Vector3 position)
	{
		var thirst = componentPlayer.Entity.FindComponent<ComponentThirst>();
		if (thirst == null)
		{
			Log.Warning("ComponentThirst no está definido en la plantilla del jugador.");
		}
		return false;
	}
}
