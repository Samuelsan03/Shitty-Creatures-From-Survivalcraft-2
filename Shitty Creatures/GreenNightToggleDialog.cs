using System;
using System.Xml.Linq;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class GreenNightToggleDialog : Dialog
	{
		private SubsystemGreenNightSky m_subsystemGreenNightSky;
		private ComponentPlayer m_player;
		private CheckboxWidget m_checkbox;
		private ButtonWidget m_daysButton;
		private ButtonWidget m_okButton;
		private ButtonWidget m_cancelButton;
		private LabelWidget m_titleLabel;
		private LabelWidget m_explanationLabel;
		private LabelWidget m_intervalLabel;
		private LabelWidget m_intervalHintLabel;
		private bool m_lastCheckState;
		private int m_originalIntervalDays;
		private int m_tempIntervalDays;
		private DifficultyMode m_originalDifficulty;
		private DifficultyMode m_tempDifficulty;

		private PaintButton m_paintButton;
		private bool m_hasExtremeUnlocked;

		public GreenNightToggleDialog(SubsystemGreenNightSky greenNightSky, ComponentPlayer player)
		{
			m_subsystemGreenNightSky = greenNightSky;
			m_player = player;
			m_originalIntervalDays = m_subsystemGreenNightSky.GreenNightIntervalDays;
			m_tempIntervalDays = m_originalIntervalDays;
			m_originalDifficulty = m_subsystemGreenNightSky.DifficultyMode;
			m_tempDifficulty = m_originalDifficulty;

			XElement node = ContentManager.Get<XElement>("Dialogs/GreenNightToggleDialog");
			LoadContents(this, node);

			m_titleLabel = Children.Find<LabelWidget>("TitleLabel", true);
			m_checkbox = Children.Find<CheckboxWidget>("Checkbox", true);
			m_daysButton = Children.Find<ButtonWidget>("DaysButton", true);
			m_explanationLabel = Children.Find<LabelWidget>("ExplanationLabel", true);
			m_okButton = Children.Find<ButtonWidget>("OKButton", true);
			m_cancelButton = Children.Find<ButtonWidget>("CancelButton", true);
			m_intervalLabel = Children.Find<LabelWidget>("IntervalLabel", true);
			m_intervalHintLabel = Children.Find<LabelWidget>("IntervalHintLabel", true);

			if (m_daysButton is BevelledButtonWidget bevelledDaysButton)
			{
				bevelledDaysButton.BevelColor = new Color(39, 146, 0);
				bevelledDaysButton.CenterColor = new Color(39, 146, 0);
			}

			m_checkbox.IsChecked = m_subsystemGreenNightSky.GreenNightEnabled;
			m_lastCheckState = m_checkbox.IsChecked;

			UpdateExplanationText();

			// ===== BOTÓN DE PINTURA (solo visible si se completó Extreme) =====
			// Verificar si el jugador completó Extreme
			var zombiesSpawn = m_subsystemGreenNightSky.Project.FindSubsystem<SubsystemZombiesSpawn>(true);
			if (zombiesSpawn != null)
			{
				// Usar el flag ExtremeCompletionDialogShown del SubsystemZombiesSpawn
				m_hasExtremeUnlocked = zombiesSpawn.HasExtremeCompleted;
			}

			if (m_hasExtremeUnlocked)
			{
				Subtexture paintTexture = ContentManager.Get<Subtexture>("Textures/Gui/pintura");
				if (paintTexture != null)
				{
					m_paintButton = new PaintButton
					{
						Subtexture = paintTexture,
						Size = new Vector2(32, 32),
						HorizontalAlignment = WidgetAlignment.Far,
						VerticalAlignment = WidgetAlignment.Near,
						MarginLeft = 0,
						MarginTop = 10,
						MarginRight = 15,
						MarginBottom = 0,
						SoundName = "Audio/Click"
					};
					Children.Add(m_paintButton);
				}
				else
				{
					Log.Error("[GreenNightToggleDialog] No se encontró la textura 'Textures/Gui/pintura'");
				}
			}
		}

		private void UpdateExplanationText()
		{
			string explanationKey = m_checkbox.IsChecked ? "EnableExplanation" : "DisableExplanation";
			m_explanationLabel.Text = LanguageControl.GetContentWidgets("GreenNightToggleDialog", explanationKey);
		}

		public override void Update()
		{
			if (m_checkbox.IsChecked != m_lastCheckState)
			{
				m_lastCheckState = m_checkbox.IsChecked;
				UpdateExplanationText();
			}

			// ===== MANEJAR CLIC EN EL BOTÓN DE PINTURA =====
			if (m_paintButton != null && m_paintButton.IsClicked)
			{
				// ELIMINADO: AudioManager.PlaySound("Audio/Rocket Knight Adventures Stage Clear", ...);

				// Mostrar el diálogo de completado Extreme (sin sonido adicional)
				var dialog = new ExtremeCompletionDialog(
					m_subsystemGreenNightSky,
					m_player,
					() =>
					{
						// ACCIÓN AL ACEPTAR
						m_subsystemGreenNightSky.DifficultyMode = DifficultyMode.Impossible;
						ShittyCreaturesModLoader.NotifyDifficultyChanged(m_subsystemGreenNightSky);
						var zombiesSpawn = m_subsystemGreenNightSky.Project.FindSubsystem<SubsystemZombiesSpawn>(true);
						if (zombiesSpawn != null)
						{
							zombiesSpawn.ResetWaves();
							zombiesSpawn.ForceUpdateDifficultyLabel();
						}
						m_player.ComponentGui.DisplaySmallMessage(
							"Has aceptado el nuevo desafío. Las oleadas se han reiniciado.",
							new Color(0, 255, 0), false, true);
					},
					showRejectMessage: true
				);
				DialogsManager.ShowDialog(m_player.GuiWidget, dialog);
			}

			if (m_daysButton.IsClicked)
			{
				var intervalDialog = new GreenNightIntervalDialog(
					m_subsystemGreenNightSky,
					m_player,
					(selectedDays, selectedDifficulty) => {
						m_tempIntervalDays = selectedDays;
						m_tempDifficulty = selectedDifficulty;
					},
					false,
					false
				);
				DialogsManager.ShowDialog(m_player.GuiWidget, intervalDialog);
			}
			else if (m_okButton.IsClicked)
			{
				Accept();
			}
			else if (m_cancelButton.IsClicked || Input.Cancel)
			{
				AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
				Dismiss();
			}
		}

		private void Accept()
		{
			bool oldValue = m_subsystemGreenNightSky.GreenNightEnabled;
			bool newValue = m_checkbox.IsChecked;
			bool enabledChanged = oldValue != newValue;

			if (enabledChanged)
				m_subsystemGreenNightSky.GreenNightEnabled = newValue;

			bool intervalChanged = m_tempIntervalDays != m_originalIntervalDays;
			if (intervalChanged)
				m_subsystemGreenNightSky.GreenNightIntervalDays = m_tempIntervalDays;

			bool difficultyChanged = m_tempDifficulty != m_originalDifficulty;
			if (difficultyChanged)
			{
				m_subsystemGreenNightSky.DifficultyMode = m_tempDifficulty;
				ShittyCreaturesModLoader.NotifyDifficultyChanged(m_subsystemGreenNightSky);
			}

			// Mostrar mensaje apropiado
			if (m_player != null && m_player.ComponentGui != null)
			{
				bool onlyEnabledChanged = enabledChanged && !intervalChanged && !difficultyChanged;

				if (onlyEnabledChanged)
				{
					// Solo se activó/desactivó la noche verde
					string key = newValue ? "EnabledNotification" : "DisabledNotification";
					string message = LanguageControl.GetContentWidgets("GreenNightToggleDialog", key);
					Color color = newValue ? Color.DarkGreen : Color.LightGreen;
					m_player.ComponentGui.DisplaySmallMessage(message, color, false, true);
				}
				else if (enabledChanged || intervalChanged || difficultyChanged)
				{
					// Hubo cambios en días o dificultad (con o sin cambio del checkbox)
					string difficultyName = GetDifficultyName(m_tempDifficulty);
					string message = string.Format(
						LanguageControl.GetContentWidgets("GreenNightIntervalDialog", "11"),
						difficultyName,
						m_tempIntervalDays);
					m_player.ComponentGui.DisplaySmallMessage(message, Color.White, false, true);
				}
			}

			// Forzar actualización del label de dificultad en el HUD
			var zombiesSpawn = m_subsystemGreenNightSky.Project.FindSubsystem<SubsystemZombiesSpawn>(true);
			if (zombiesSpawn != null)
			{
				zombiesSpawn.ForceUpdateDifficultyLabel();
			}

			AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
			Dismiss();
		}

		private string GetDifficultyName(DifficultyMode mode)
		{
			string key = mode switch
			{
				DifficultyMode.VeryEasy => "VeryEasy_Name",
				DifficultyMode.Easy => "Easy_Name",
				DifficultyMode.Normal => "Normal_Name",
				DifficultyMode.Medium => "Medium_Name",
				DifficultyMode.Hard => "Hard_Name",
				DifficultyMode.Extreme => "Extreme_Name",
				DifficultyMode.Impossible => "Impossible_Name",
				_ => "Normal_Name"
			};
			return LanguageControl.GetContentWidgets("GreenNightDifficulty", key);
		}

		private void Dismiss()
		{
			DialogsManager.HideDialog(this);
		}

		// ===== CLASE AUXILIAR PARA EL BOTÓN DE PINTURA =====
		private class PaintButton : ClickableWidget
		{
			public Subtexture Subtexture;
			private Vector2 m_size;
			public Vector2 Size
			{
				get => m_size;
				set { m_size = value; }
			}
			public string SoundName;

			public override void MeasureOverride(Vector2 parentAvailableSize)
			{
				base.DesiredSize = m_size;
				base.IsDrawRequired = true;
				base.IsHitTestVisible = true;
			}

			public override void Draw(Widget.DrawContext dc)
			{
				if (Subtexture != null && Subtexture.Texture != null)
				{
					TexturedBatch2D batch = dc.PrimitivesRenderer2D.TexturedBatch(Subtexture.Texture, false, 0, DepthStencilState.None, null, BlendState.AlphaBlend, SamplerState.PointClamp);
					int count = batch.TriangleVertices.Count;
					Vector2 texCoord1 = Subtexture.TopLeft;
					Vector2 texCoord2 = Subtexture.BottomRight;
					batch.QueueQuad(Vector2.Zero, m_size, 0f, texCoord1, texCoord2, base.GlobalColorTransform);
					batch.TransformTriangles(base.GlobalTransform, count, -1);
				}
			}
		}
	}
}
