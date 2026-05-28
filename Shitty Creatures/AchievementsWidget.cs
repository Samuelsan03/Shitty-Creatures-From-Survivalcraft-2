using System;
using System.Xml.Linq;
using Engine;
using Game;

namespace Game
{
    public class AchievementsWidget : CanvasWidget
    {
        private ComponentPlayer m_componentPlayer;
        private BevelledButtonWidget m_closeButton;
        private StackPanelWidget m_achievementsStack;

        public AchievementsWidget(ComponentPlayer player)
        {
            m_componentPlayer = player;
            XElement node = ContentManager.Get<XElement>("Widgets/AchievementsWidget");
            LoadContents(this, node);
            m_closeButton = Children.Find<BevelledButtonWidget>("CloseButton", true);
            m_achievementsStack = Children.Find<StackPanelWidget>("AchievementsStack", true);

			CreateAchievementItem("Mata un Tank", "Elimina un Tank con cualquier arma (cuerpo a cuerpo o distancia)", ShittyCreaturesModLoader.IsAchievementUnlocked(m_componentPlayer, 1));
		}

        private void CreateAchievementItem(string title, string description, bool unlocked)
        {
            var achievementContainer = new CanvasWidget
            {
                Size = new Vector2(530, 70),
                Margin = new Vector2(0, 3)
            };

            var background = new BevelledRectangleWidget
            {
                Size = new Vector2(530, 70),
                BevelSize = 2,
                CenterColor = unlocked ? new Color(0, 40, 0, 200) : new Color(40, 40, 40, 200),
                BevelColor = unlocked ? new Color(0, 100, 0) : new Color(80, 80, 80)
            };
            achievementContainer.Children.Add(background);

            // Estado (esquina superior izquierda)
            var statusLabel = new LabelWidget
            {
                Text = unlocked ? "COMPLETADO" : "PENDIENTE",
                Color = unlocked ? Color.Green : Color.Red,
                FontScale = 0.7f,
                HorizontalAlignment = WidgetAlignment.Near,
                VerticalAlignment = WidgetAlignment.Near,
                Margin = new Vector2(10, 5)
            };
            achievementContainer.Children.Add(statusLabel);

            // Título (centrado arriba)
            var titleLabel = new LabelWidget
            {
                Text = title,
                Color = unlocked ? Color.White : new Color(180, 180, 180),
                FontScale = 0.9f,
                HorizontalAlignment = WidgetAlignment.Center,
                VerticalAlignment = WidgetAlignment.Near,
                Margin = new Vector2(0, 5)
            };
            achievementContainer.Children.Add(titleLabel);

            // Descripción (centrado medio)
            var descLabel = new LabelWidget
            {
                Text = description,
                Color = unlocked ? new Color(200, 200, 200) : new Color(140, 140, 140),
                FontScale = 0.65f,
                HorizontalAlignment = WidgetAlignment.Center,
                VerticalAlignment = WidgetAlignment.Center,
                Margin = new Vector2(0, 30)
            };
            achievementContainer.Children.Add(descLabel);

            m_achievementsStack.Children.Add(achievementContainer);
        }

        public override void Update()
        {
            if (m_closeButton.IsClicked)
            {
                m_componentPlayer.ComponentGui.ModalPanelWidget = null;
            }
        }
    }
}
