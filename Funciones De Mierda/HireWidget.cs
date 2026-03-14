using System;
using System.Xml.Linq;
using Engine;
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
		private RectangleWidget m_coinIcon;

		public HireWidget(ComponentPlayer player, ComponentHireableNPC hireable)
		{
			m_player = player;
			m_hireable = hireable;
			m_subsystemAudio = player.Project.FindSubsystem<SubsystemAudio>(true);

			// Cargar el XML del widget
			XElement node = ContentManager.Get<XElement>("Widgets/HireWidget");
			LoadContents(this, node);

			// Obtener referencias a los widgets
			m_titleLabel = Children.Find<LabelWidget>("HireTitleLabel", true);
			m_loreLabel = Children.Find<LabelWidget>("HireLoreLabel", true);
			m_infoLabel = Children.Find<LabelWidget>("HireInfoLabel", true);
			m_hireButton = Children.Find<BevelledButtonWidget>("HireButton", true);
			m_coinIcon = Children.Find<RectangleWidget>("CoinIcon", true);

			// Cargar textos desde LanguageControl
			m_titleLabel.Text = LanguageControl.GetContentWidgets("HireWidget", "Title");
			m_hireButton.Text = LanguageControl.GetContentWidgets("HireWidget", "HireButton");

			string loreFormat = LanguageControl.GetContentWidgets("HireWidget", "LoreText");
			m_loreLabel.Text = string.Format(loreFormat, hireable.HirePrice);

			string infoFormat = LanguageControl.GetContentWidgets("HireWidget", "InfoText");
			m_infoLabel.Text = string.Format(infoFormat, hireable.HirePrice);
		}

		public override void Update()
		{
			// Cerrar el widget si el NPC ha desaparecido o el jugador ha muerto
			if (!m_hireable.IsAddedToProject || m_player.ComponentHealth.Health == 0f)
			{
				ParentWidget.Children.Remove(this);
				return;
			}

			if (m_hireButton.IsClicked)
			{
				if (m_hireable.TryHire(m_player))
				{
					// Cerrar el widget si la contratación fue exitosa
					ParentWidget.Children.Remove(this);
				}
				// Si falla (ej. no hay suficientes monedas), el widget permanece abierto
				// El mensaje de error ya lo muestra TryHire
			}

			base.Update();
		}
	}
}
