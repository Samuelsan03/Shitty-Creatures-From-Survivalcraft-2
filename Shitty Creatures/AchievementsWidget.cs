using System.Xml.Linq;
using Engine;
using Game;

namespace Game
{
	public class AchievementsWidget : CanvasWidget
	{
		private ComponentPlayer m_componentPlayer;
		private BevelledButtonWidget m_closeButton;

		public AchievementsWidget(ComponentPlayer player)
		{
			m_componentPlayer = player;
			XElement node = ContentManager.Get<XElement>("Widgets/AchievementsWidget");
			LoadContents(this, node);
			m_closeButton = Children.Find<BevelledButtonWidget>("CloseButton", true);
		}

		public override void Update()
		{
			if (m_closeButton.IsClicked)
			{
				// Cerrar el panel modal
				m_componentPlayer.ComponentGui.ModalPanelWidget = null;
			}
		}
	}
}
