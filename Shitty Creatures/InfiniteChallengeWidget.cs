using System;
using System.Xml.Linq;
using Engine;
using Engine.Input;
using Game;

namespace Game
{
	public class InfiniteChallengeWidget : CanvasWidget
	{
		private ComponentPlayer m_componentPlayer;
		private ButtonWidget m_acceptButton;
		private ButtonWidget m_rejectButton;
		private Action<bool> m_onComplete;
		private bool m_completed;

		public InfiniteChallengeWidget(ComponentPlayer componentPlayer, Action<bool> onComplete)
		{
			m_componentPlayer = componentPlayer;
			m_onComplete = onComplete;

			XElement node = ContentManager.Get<XElement>("Widgets/InfiniteChallengeWidget");
			LoadContents(this, node);

			m_acceptButton = Children.Find<ButtonWidget>("AcceptButton", true);
			m_rejectButton = Children.Find<ButtonWidget>("RejectButton", true);
		}

		public override void Update()
		{
			if (m_completed) return;

			if (Input.IsKeyDown(Key.Escape))
			{
				Close(false);
				return;
			}

			if (m_acceptButton.IsClicked)
			{
				Close(true);
			}
			else if (m_rejectButton.IsClicked)
			{
				Close(false);
			}
		}

		private void Close(bool result)
		{
			if (m_completed) return;
			m_completed = true;

			Action<bool> callback = m_onComplete;
			m_onComplete = null;

			if (m_componentPlayer?.ComponentGui != null &&
				m_componentPlayer.ComponentGui.ModalPanelWidget == this)
			{
				m_componentPlayer.ComponentGui.ModalPanelWidget = null;
			}

			callback?.Invoke(result);
		}

		public static void Show(ComponentPlayer player, Action<bool> onComplete)
		{
			if (player?.ComponentGui == null) return;

			if (player.ComponentGui.ModalPanelWidget is InfiniteChallengeWidget existing)
			{
				existing.m_completed = true;
				existing.m_onComplete = null;
			}

			player.ComponentGui.ModalPanelWidget = new InfiniteChallengeWidget(player, onComplete);
		}
	}
}
