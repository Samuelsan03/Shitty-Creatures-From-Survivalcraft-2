using System;
using Engine;
using Game;

namespace Game
{
	/// <summary>
	/// Diálogo de advertencia estilo Survivalcraft que se muestra al activar la guerra
	/// mientras la Noche Verde está activa o programada.
	/// </summary>
	public class GreenNightWarConflictDialog : Dialog
	{
		private ComponentPlayer m_player;
		private Action m_onContinue;
		private ButtonWidget m_continueButton;
		private ButtonWidget m_cancelButton;

		public GreenNightWarConflictDialog(ComponentPlayer player, Action onContinue)
		{
			m_player = player ?? throw new ArgumentNullException(nameof(player));
			m_onContinue = onContinue;

			// Obtener información de la Noche Verde
			SubsystemGreenNightSky greenNight = player.Project.FindSubsystem<SubsystemGreenNightSky>(true);
			DifficultyMode difficulty = greenNight != null ? greenNight.DifficultyMode : DifficultyMode.Normal;
			bool isExtreme = difficulty == DifficultyMode.Extreme;

			// Obtener nombre de la dificultad desde el sistema de idioma
			string difficultyName = GetDifficultyName(difficulty);

			// Obtener textos del LanguageControl
			string title = LanguageControl.Get("GreenNightWarConflictDialog", "1");
			string messageText;
			if (isExtreme)
			{
				messageText = string.Format(
					LanguageControl.Get("GreenNightWarConflictDialog", "2"),
					difficultyName
				);
			}
			else
			{
				// Mensaje sin la parte de EXTREMA
				string baseMessage = LanguageControl.Get("GreenNightWarConflictDialog", "2");
				string extremePart = LanguageControl.Get("GreenNightWarConflictDialog", "3");
				// Remover la parte de EXTREMA del mensaje base
				messageText = baseMessage.Replace(extremePart, "");
			}
			string continueText = LanguageControl.Get("GreenNightWarConflictDialog", "5");
			string cancelText = LanguageControl.Get("GreenNightWarConflictDialog", "6");

			// Tamaño del diálogo
			Vector2 dialogSize = new Vector2(620f, 480f);

			// Canvas principal
			CanvasWidget canvas = new CanvasWidget
			{
				Size = dialogSize,
				HorizontalAlignment = WidgetAlignment.Center,
				VerticalAlignment = WidgetAlignment.Center,
				IsHitTestVisible = true
			};

			// Fondo oscuro con borde blanco
			RectangleWidget background = new RectangleWidget
			{
				Size = dialogSize,
				FillColor = new Color(0, 0, 0, 220),
				OutlineColor = Color.White,
				OutlineThickness = 2f,
				IsHitTestVisible = true
			};
			canvas.Children.Add(background);

			// Contenedor vertical principal
			StackPanelWidget mainStack = new StackPanelWidget
			{
				Direction = LayoutDirection.Vertical,
				HorizontalAlignment = WidgetAlignment.Center,
				VerticalAlignment = WidgetAlignment.Center,
				Margin = new Vector2(25f, 20f)
			};

			// Título
			LabelWidget titleLabel = new LabelWidget
			{
				Text = title,
				FontScale = 1.8f,
				Color = Color.Red,
				HorizontalAlignment = WidgetAlignment.Center,
				Margin = new Vector2(0f, 5f)
			};
			mainStack.Children.Add(titleLabel);

			// Canvas contenedor para el ScrollPanel
			CanvasWidget scrollContainer = new CanvasWidget
			{
				Size = new Vector2(560f, 210f),
				HorizontalAlignment = WidgetAlignment.Center,
				Margin = new Vector2(0f, 5f)
			};

			// ScrollPanelWidget sin Size (heredará el tamaño del canvas contenedor)
			ScrollPanelWidget scrollPanel = new ScrollPanelWidget
			{
				Direction = LayoutDirection.Vertical,
				ClampToBounds = true
			};

			LabelWidget message = new LabelWidget
			{
				Text = messageText,
				FontScale = 0.75f,
				Color = Color.White,
				HorizontalAlignment = WidgetAlignment.Center,
				Margin = new Vector2(10f, 10f)
			};

			// Agregar el label al scroll panel
			scrollPanel.Children.Add(message);

			// Agregar el scroll panel al canvas contenedor
			scrollContainer.Children.Add(scrollPanel);

			// Agregar el canvas contenedor al stack principal
			mainStack.Children.Add(scrollContainer);

			// Fila de botones
			StackPanelWidget buttonRow = new StackPanelWidget
			{
				Direction = LayoutDirection.Horizontal,
				HorizontalAlignment = WidgetAlignment.Center,
				Margin = new Vector2(0f, 15f)
			};

			// Botón "Continuar" (verde)
			BevelledButtonWidget continueBtn = new BevelledButtonWidget
			{
				Text = continueText,
				Size = new Vector2(160f, 50f),
				BevelColor = new Color(0, 180, 0),
				CenterColor = new Color(0, 180, 0),
				Margin = new Vector2(15f, 0f)
			};
			buttonRow.Children.Add(continueBtn);

			// Botón "Cancelar" (rojo)
			BevelledButtonWidget cancelBtn = new BevelledButtonWidget
			{
				Text = cancelText,
				Size = new Vector2(160f, 50f),
				BevelColor = new Color(180, 0, 0),
				CenterColor = new Color(180, 0, 0),
				Margin = new Vector2(15f, 0f)
			};
			buttonRow.Children.Add(cancelBtn);

			mainStack.Children.Add(buttonRow);

			// Agregar el stack al canvas
			canvas.Children.Add(mainStack);

			// Guardar referencias
			m_continueButton = continueBtn;
			m_cancelButton = cancelBtn;

			Children.Add(canvas);
		}

		private string GetDifficultyName(DifficultyMode mode)
		{
			// Usa EXACTAMENTE las mismas claves que GreenNightIntervalDialog
			string key = mode switch
			{
				DifficultyMode.VeryEasy => "VeryEasy_Name",
				DifficultyMode.Easy => "Easy_Name",
				DifficultyMode.Normal => "Normal_Name",
				DifficultyMode.Medium => "Medium_Name",
				DifficultyMode.Hard => "Hard_Name",
				DifficultyMode.Extreme => "Extreme_Name",
				_ => "Normal_Name"
			};
			return LanguageControl.GetContentWidgets("GreenNightDifficulty", key);
		}

		public override void Update()
		{
			if (m_continueButton.IsClicked)
			{
				AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
				m_onContinue?.Invoke();
				DialogsManager.HideDialog(this);
			}
			else if (m_cancelButton.IsClicked || Input.Cancel)
			{
				AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
				DialogsManager.HideDialog(this);
			}

			base.Update();
		}
	}
}
