using System;
using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	// Token: 0x0200008E RID: 142
	public class ComponentMusic : Component, IUpdateable
	{
		// Token: 0x17000037 RID: 55
		// (get) Token: 0x0600042F RID: 1071 RVA: 0x00013AD8 File Offset: 0x00011CD8
		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		// Token: 0x06000430 RID: 1072 RVA: 0x00013ADC File Offset: 0x00011CDC
		public void Update(float dt)
		{
			if (!this.MusicButton.IsClicked)
			{
				return;
			}
			if (this.m_openMusic)
			{
				this.m_openMusic = false;
				MusicManagerN.StopMusic();
				this.m_componentPlayer.ComponentGui.DisplaySmallMessage("Background music turned off", Color.White, true, true, 1f);
				return;
			}
			this.m_openMusic = true;
			this.m_componentPlayer.ComponentGui.DisplaySmallMessage("Background music turned on", Color.White, true, true, 1f);
			Random random = new Random();
			MusicManagerN.PlayMusic(random.Bool(0.33f) ? "Music/Touhou2RecordoftheSealingofanOrientalDemon" : (random.Bool(0.33f) ? "Music/Touhou2博麗EasternWind" : "Music/Touhou2MimasThemeCompleteDarkness"), 0f);
		}

		// Token: 0x06000431 RID: 1073 RVA: 0x00013B92 File Offset: 0x00011D92
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			this.m_componentPlayer = base.Entity.FindComponent<ComponentPlayer>(true);
			this.IsButtonVisible = valuesDictionary.GetValue<bool>("MusicButtonVisible");
		}

		// Token: 0x06000432 RID: 1074 RVA: 0x00013BB8 File Offset: 0x00011DB8
		public override void OnEntityAdded()
		{
			ContainerWidget rightControlsContainerWidget = this.m_componentPlayer.ComponentGui.m_rightControlsContainerWidget;
			this.MusicButton = rightControlsContainerWidget.Children.Find<BevelledButtonWidget>("MusicButton", false);
			if (this.MusicButton != null)
			{
				return;
			}
			this.MusicButton = new BevelledButtonWidget
			{
				Name = "MusicButton",
				Text = "Music",
				Size = new Vector2(88f, 56f),
				IsEnabled = true,
				IsVisible = this.IsButtonVisible,
				HorizontalAlignment = WidgetAlignment.Far,
				IsAutoCheckingEnabled = true
			};
			this.MusicButton.m_labelWidget.FontScale = 0.8f;
			rightControlsContainerWidget.Children.Add(this.MusicButton);
		}

		// Token: 0x040001BA RID: 442
		public ComponentPlayer m_componentPlayer;

		// Token: 0x040001BB RID: 443
		public BevelledButtonWidget MusicButton;

		// Token: 0x040001BC RID: 444
		public bool IsButtonVisible;

		// Token: 0x040001BD RID: 445
		private bool m_openMusic;
	}
}
