using System;
using System.Xml.Linq;
using Engine;
using Engine.Graphics;
using Engine.Media;
using GameEntitySystem;

namespace Game
{
	public class HireWidget : CanvasWidget
	{
		private ComponentPlayer m_player;
		private ComponentHireableNPC m_hireable;
		private SubsystemAudio m_subsystemAudio;

		private BevelledButtonWidget m_hireButton;
		private LabelWidget m_titleLabel;
		private LabelWidget m_infoLabel;
		private LabelWidget m_loreLabel;
		private BlockIconWidget m_coinIcon; // Cambiado a BlockIconWidget

		public HireWidget(ComponentPlayer player, ComponentHireableNPC hireable)
		{
			m_player = player;
			m_hireable = hireable;
			m_subsystemAudio = player.Project.FindSubsystem<SubsystemAudio>(true);

			XElement node = ContentManager.Get<XElement>("Widgets/HireWidget");
			LoadContents(this, node);

			m_titleLabel = Children.Find<LabelWidget>("HireTitleLabel", true);
			m_loreLabel = Children.Find<LabelWidget>("HireLoreLabel", true);
			m_infoLabel = Children.Find<LabelWidget>("HireInfoLabel", true);
			m_hireButton = Children.Find<BevelledButtonWidget>("HireButton", true);
			m_coinIcon = Children.Find<BlockIconWidget>("CoinIcon", true); // Cambiado

			m_titleLabel.Text = LanguageControl.GetContentWidgets("HireWidget", "Title");
			m_hireButton.Text = LanguageControl.GetContentWidgets("HireWidget", "HireButton");

			string loreFormat = LanguageControl.GetContentWidgets("HireWidget", "LoreText");
			m_loreLabel.Text = string.Format(loreFormat, hireable.HirePrice);

			string infoFormat = LanguageControl.GetContentWidgets("HireWidget", "InfoText");
			m_infoLabel.Text = string.Format(infoFormat, hireable.HirePrice);

			// Asignar el valor del bloque para que BlockIconWidget muestre la textura correcta
			int coinBlockIndex = BlocksManager.GetBlockIndex<NuclearCoinBlock>(true);
			m_coinIcon.Value = Terrain.MakeBlockValue(coinBlockIndex);
		}

		public override void Update()
		{
			if (!m_hireable.IsAddedToProject || m_player.ComponentHealth.Health == 0f)
			{
				ParentWidget.Children.Remove(this);
				return;
			}

			if (m_hireButton.IsClicked)
			{
				if (m_hireable.TryHire(m_player))
				{
					ParentWidget.Children.Remove(this);
				}
			}

			base.Update();
		}
	}
}
