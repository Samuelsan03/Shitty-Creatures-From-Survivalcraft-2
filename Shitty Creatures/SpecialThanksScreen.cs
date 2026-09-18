using System.Collections.Generic;
using System.Xml.Linq;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class SpecialThanksScreen : Screen
	{
		private ScrollPanelWidget m_scrollPanel;

		// Lista de texturas (opcional). Si quieres que cada persona tenga su PFP,
		// el orden debe coincidir con el orden de los nombres en el array "thanks".
		// Si no hay textura para alguien, deja null o una ruta inválida.
		private static readonly string[] PfpTextures =
		{
			"Textures/Agradecimientos/quotemante",
			"Textures/Agradecimientos/Richard Survivalcraft",
			"Textures/Agradecimientos/josh",
			// Agrega más rutas aquí si añades más personas
		};

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

			// ----- LEER ARRAY "thanks" DEL JSON -----
			// Formato: [nombre, razón, nombre, razón, ...]
			List<string> thanksList = new List<string>();
			bool found;
			int i = 0;
			while (true)
			{
				string value = LanguageControl.Get(out found, "SpecialThanksScreen", "thanks", i.ToString());
				if (!found) break;
				thanksList.Add(value);
				i++;
			}

			// Recorremos de dos en dos: nombre + razón
			int personIndex = 0;
			for (int j = 0; j + 1 < thanksList.Count; j += 2)
			{
				string name = thanksList[j];
				string reason = thanksList[j + 1];

				var personPanel = new StackPanelWidget
				{
					Direction = LayoutDirection.Vertical,
					HorizontalAlignment = WidgetAlignment.Center,
					Margin = new Vector2(0, 10)
				};

				// Nombre
				personPanel.Children.Add(new LabelWidget
				{
					Text = name,
					FontScale = 1f,
					Color = Color.White,
					HorizontalAlignment = WidgetAlignment.Center,
					DropShadow = true
				});

				// PFP (si hay textura definida para esta posición)
				Texture2D texture = null;
				if (personIndex < PfpTextures.Length && !string.IsNullOrEmpty(PfpTextures[personIndex]))
				{
					try { texture = ContentManager.Get<Texture2D>(PfpTextures[personIndex]); } catch { }
				}

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

				// Razón
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
				personIndex++;
			}

			// ----- SEPARADOR -----
			thanksStack.Children.Add(new CanvasWidget { Size = new Vector2(0, 15) });

			// ----- MENSAJE DE CONTACTO AL FINAL (índice 1) -----
			thanksStack.Children.Add(new LabelWidget
			{
				Text = LanguageControl.Get("SpecialThanksScreen", 1),
				FontScale = 0.75f,
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
