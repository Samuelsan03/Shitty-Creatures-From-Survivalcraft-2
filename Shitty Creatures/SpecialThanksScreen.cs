using System.Xml.Linq;
using Engine;
using Engine.Graphics;

namespace Game
{
    public class SpecialThanksScreen : Screen
    {
        private ScrollPanelWidget m_scrollPanel;

        public SpecialThanksScreen()
        {
            XElement node = ContentManager.Get<XElement>("Screens/SpecialThanksScreen");
            this.LoadContents(this, node);
            m_scrollPanel = this.Children.Find<ScrollPanelWidget>("ScrollPanel", true);
        }

        public override void Enter(object[] parameters)
        {
            // Título de la barra superior (índice 0)
            LabelWidget topBarLabel = this.Children.Find<LabelWidget>("TopBar.Label", true);
            if (topBarLabel != null)
                topBarLabel.Text = LanguageControl.Get("SpecialThanksScreen", 0);

            // Stack de agradecimientos
            StackPanelWidget thanksStack = this.Children.Find<StackPanelWidget>("ThanksStack", true);
            if (thanksStack == null) return;
            thanksStack.Children.Clear();

            // ----- TÍTULO DORADO "Special Thanks" -----
            thanksStack.Children.Add(new LabelWidget
            {
                Text = LanguageControl.Get("SpecialThanksScreen", 0),
                FontScale = 1.5f,
                Color = new Color(255, 215, 0), // Dorado
                HorizontalAlignment = WidgetAlignment.Center,
                DropShadow = true,
                Margin = new Vector2(0, 0)
            });

            thanksStack.Children.Add(new CanvasWidget { Size = new Vector2(0, 15) }); // Espaciado

            // Cada entrada: (índice del nombre, ruta de la textura)
            // La razón será el índice+1
            var personKeys = new (int NameIndex, string TexturePath)[]
            {
                (2, "Textures/Agradecimientos/quotemante"),
                // Siguiente persona: (4, "Textures/Agradecimientos/otro"),
            };

            foreach (var person in personKeys)
            {
                var personPanel = new StackPanelWidget
                {
                    Direction = LayoutDirection.Vertical,
                    HorizontalAlignment = WidgetAlignment.Center,
                    Margin = new Vector2(0, 10)
                };

                // Nombre (índice par: 2, 4, 6...)
                string name = LanguageControl.Get("SpecialThanksScreen", person.NameIndex);
                personPanel.Children.Add(new LabelWidget
                {
                    Text = name,
                    FontScale = 1f,
                    Color = Color.White,
                    HorizontalAlignment = WidgetAlignment.Center,
                    DropShadow = true
                });

                // PFP
                Texture2D texture = null;
                try { texture = ContentManager.Get<Texture2D>(person.TexturePath); } catch { }

                personPanel.Children.Add(new RectangleWidget
                {
                    Size = new Vector2(64f, 64f),
                    Subtexture = (texture != null) ? new Subtexture(texture) : null,
                    FillColor = (texture != null) ? Color.White : Color.Gray,
                    OutlineColor = Color.Transparent,
                    HorizontalAlignment = WidgetAlignment.Center,
                    VerticalAlignment = WidgetAlignment.Center,
                    TextureLinearFilter = true,
                    IsVisible = true
                });

                // Razón (índice impar: 3, 5, 7...)
                string reason = LanguageControl.Get("SpecialThanksScreen", person.NameIndex + 1);
                personPanel.Children.Add(new LabelWidget
                {
                    Text = reason,
                    FontScale = 0.7f,
                    Color = new Color(192, 192, 192),
                    HorizontalAlignment = WidgetAlignment.Center,
                    TextAnchor = TextAnchor.HorizontalCenter,
                    WordWrap = true,
                    Margin = new Vector2(0, 5)
                });

                thanksStack.Children.Add(personPanel);
            }

            // ----- SEPARADOR -----
            thanksStack.Children.Add(new CanvasWidget { Size = new Vector2(0, 15) });

            // ----- MENSAJE DE CONTACTO AL FINAL (índice 1) -----
            thanksStack.Children.Add(new LabelWidget
            {
                Text = LanguageControl.Get("SpecialThanksScreen", 1),
                FontScale = 0.6f,
                Color = new Color(160, 160, 160),
                HorizontalAlignment = WidgetAlignment.Center,
                TextAnchor = TextAnchor.HorizontalCenter,
                WordWrap = true,
                Margin = new Vector2(20, 10)
            });

            m_scrollPanel.ScrollPosition = 0f;
        }

        public override void Update()
        {
            if (Input.Back || Input.Cancel ||
                this.Children.Find<ButtonWidget>("TopBar.Back", true).IsClicked)
            {
                ScreensManager.SwitchScreen(ScreensManager.PreviousScreen, new object[0]);
            }
        }
    }
}
