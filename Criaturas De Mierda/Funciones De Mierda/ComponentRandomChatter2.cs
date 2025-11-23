using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Engine;
using Engine.Graphics;
using Engine.Media;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	// Token: 0x02000009 RID: 9
	[NullableContext(1)]
	[Nullable(0)]
	public class ComponentRandomChatter2 : Component, IUpdateable, IDrawable
	{
		// Token: 0x1700000E RID: 14
		// (get) Token: 0x06000056 RID: 86 RVA: 0x000064B0 File Offset: 0x000046B0
		public int[] DrawOrders
		{
			get
			{
				return new int[]
				{
					2000
				};
			}
		}

		// Token: 0x1700000F RID: 15
		// (get) Token: 0x06000057 RID: 87 RVA: 0x000064D0 File Offset: 0x000046D0
		// (set) Token: 0x06000058 RID: 88 RVA: 0x000064D8 File Offset: 0x000046D8
		public float ActivationDistance { get; set; }

		// Token: 0x17000010 RID: 16
		// (get) Token: 0x06000059 RID: 89 RVA: 0x000064E1 File Offset: 0x000046E1
		// (set) Token: 0x0600005A RID: 90 RVA: 0x000064E9 File Offset: 0x000046E9
		public float DisplayDistance { get; set; }

		// Token: 0x17000011 RID: 17
		// (get) Token: 0x0600005B RID: 91 RVA: 0x000064F2 File Offset: 0x000046F2
		// (set) Token: 0x0600005C RID: 92 RVA: 0x000064FA File Offset: 0x000046FA
		public float MinInterval { get; set; }

		// Token: 0x17000012 RID: 18
		// (get) Token: 0x0600005D RID: 93 RVA: 0x00006503 File Offset: 0x00004703
		// (set) Token: 0x0600005E RID: 94 RVA: 0x0000650B File Offset: 0x0000470B
		public float MaxInterval { get; set; }

		// Token: 0x17000013 RID: 19
		// (get) Token: 0x0600005F RID: 95 RVA: 0x00006514 File Offset: 0x00004714
		// (set) Token: 0x06000060 RID: 96 RVA: 0x0000651C File Offset: 0x0000471C
		public float RiseDuration { get; set; }

		// Token: 0x17000014 RID: 20
		// (get) Token: 0x06000061 RID: 97 RVA: 0x00006525 File Offset: 0x00004725
		// (set) Token: 0x06000062 RID: 98 RVA: 0x0000652D File Offset: 0x0000472D
		public float FadeDuration { get; set; }

		// Token: 0x17000015 RID: 21
		// (get) Token: 0x06000063 RID: 99 RVA: 0x00006538 File Offset: 0x00004738
		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		// Token: 0x06000064 RID: 100 RVA: 0x0000654C File Offset: 0x0000474C
		public override void Load(ValuesDictionary values, IdToEntityMap map)
		{
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_subsystemPlayers = base.Project.FindSubsystem<SubsystemPlayers>(true);
			this.m_subsystemModelsRenderer = base.Project.FindSubsystem<SubsystemModelsRenderer>(true);
			this.m_componentBody = base.Entity.FindComponent<ComponentBody>(true);
			this.m_font = ContentManager.Get<BitmapFont>("Fonts/Pericles");
			this.ActivationDistance = values.GetValue<float>("ActivationDistance", 5f);
			this.DisplayDistance = values.GetValue<float>("DisplayDistance", 10f);
			this.MinInterval = values.GetValue<float>("MinInterval", 8f);
			this.MaxInterval = values.GetValue<float>("MaxInterval", 30f);
			this.RiseDuration = values.GetValue<float>("RiseDuration", 2f);
			this.FadeDuration = values.GetValue<float>("FadeDuration", 0.5f);
			this.SetNextChatterTime();
		}

		// Token: 0x06000065 RID: 101 RVA: 0x00006644 File Offset: 0x00004844
		public void Update(float dt)
		{
			double gameTime = this.m_subsystemTime.GameTime;
			bool flag = this.m_currentState == ComponentRandomChatter2.State.Inactive;
			if (flag)
			{
				bool flag2 = gameTime > this.m_nextChatterTime;
				if (flag2)
				{
					bool playerNear = false;
					foreach (PlayerData playerData in this.m_subsystemPlayers.PlayersData)
					{
						ComponentPlayer componentPlayer = playerData.ComponentPlayer;
						bool flag3 = ((componentPlayer != null) ? componentPlayer.ComponentBody : null) != null && Vector3.DistanceSquared(this.m_componentBody.Position, playerData.ComponentPlayer.ComponentBody.Position) < this.ActivationDistance * this.ActivationDistance;
						if (flag3)
						{
							playerNear = true;
							break;
						}
					}
					bool flag4 = playerNear;
					if (flag4)
					{
						this.m_activePhrase = ComponentRandomChatter2.m_phrases[this.m_random.Int(0, ComponentRandomChatter2.m_phrases.Count - 1)];
						this.m_currentState = ComponentRandomChatter2.State.Rising;
						this.m_riseStartTime = gameTime;
						this.SetNextChatterTime();
					}
					else
					{
						this.m_nextChatterTime = gameTime + 1.0;
					}
				}
			}
			else
			{
				bool flag5 = this.m_currentState == ComponentRandomChatter2.State.Rising;
				if (flag5)
				{
					double riseProgress = (gameTime - this.m_riseStartTime) / (double)this.RiseDuration;
					bool flag6 = riseProgress >= 1.0;
					if (flag6)
					{
						this.m_currentState = ComponentRandomChatter2.State.FadeOut;
						this.m_fadeStartTime = gameTime;
					}
				}
				else
				{
					bool flag7 = this.m_currentState == ComponentRandomChatter2.State.FadeOut;
					if (flag7)
					{
						bool flag8 = gameTime > this.m_fadeStartTime + (double)this.FadeDuration;
						if (flag8)
						{
							this.m_currentState = ComponentRandomChatter2.State.Inactive;
							this.m_activePhrase = null;
						}
					}
				}
			}
		}

		// Token: 0x06000066 RID: 102 RVA: 0x00006810 File Offset: 0x00004A10
		public void Draw(Camera camera, int drawOrder)
		{
			bool flag = this.m_currentState == ComponentRandomChatter2.State.Inactive || string.IsNullOrEmpty(this.m_activePhrase);
			if (!flag)
			{
				bool playerInRange = false;
				foreach (PlayerData playerData in this.m_subsystemPlayers.PlayersData)
				{
					ComponentPlayer componentPlayer = playerData.ComponentPlayer;
					bool flag2 = ((componentPlayer != null) ? componentPlayer.ComponentBody : null) != null && Vector3.DistanceSquared(this.m_componentBody.Position, playerData.ComponentPlayer.ComponentBody.Position) < this.DisplayDistance * this.DisplayDistance;
					if (flag2)
					{
						playerInRange = true;
						break;
					}
				}
				bool flag3 = !playerInRange;
				if (!flag3)
				{
					float alpha = 1f;
					double currentTime = this.m_subsystemTime.GameTime;
					bool flag4 = this.m_currentState == ComponentRandomChatter2.State.FadeOut;
					if (flag4)
					{
						double fadeProgress = (currentTime - this.m_fadeStartTime) / (double)this.FadeDuration;
						alpha = 1f - MathUtils.Clamp((float)fadeProgress, 0f, 1f);
					}
					double riseProgress = (currentTime - this.m_riseStartTime) / (double)this.RiseDuration;
					float progress = MathUtils.Clamp((float)riseProgress, 0f, 1f);
					FontBatch3D fontBatch = this.m_subsystemModelsRenderer.PrimitivesRenderer.FontBatch(this.m_font, 1, DepthStencilState.None, RasterizerState.CullNoneScissor, BlendState.AlphaBlend, SamplerState.LinearClamp);
					Vector3 center = (this.m_componentBody.BoundingBox.Min + this.m_componentBody.BoundingBox.Max) * 0.5f;
					float height = this.m_componentBody.BoundingBox.Max.Y - this.m_componentBody.BoundingBox.Min.Y;
					float startHeight = height * 0.3f;
					float endHeight = height * 0.9f;
					float currentHeight = startHeight + (endHeight - startHeight) * progress;
					Vector3 position = new Vector3(center.X, center.Y + currentHeight, center.Z);
					Vector3 right = Vector3.Normalize(Vector3.Cross(camera.ViewDirection, camera.ViewUp));
					float scale = 0.005f;
					Vector3 screenPos = Vector3.Transform(position, camera.ViewMatrix);
					Vector3 rightVec = Vector3.TransformNormal(right, camera.ViewMatrix) * scale;
					Vector3 downVec = Vector3.TransformNormal(-Vector3.UnitY, camera.ViewMatrix) * scale;
					Color color = Color.White * alpha;
					fontBatch.QueueText(this.m_activePhrase, screenPos, rightVec, downVec, color, TextAnchor.Center);
					fontBatch.Flush(camera.ViewProjectionMatrix, false);
				}
			}
		}

		// Token: 0x06000067 RID: 103 RVA: 0x00006AC8 File Offset: 0x00004CC8
		private void SetNextChatterTime()
		{
			this.m_nextChatterTime = this.m_subsystemTime.GameTime + (double)this.m_random.Float(this.MinInterval, this.MaxInterval);
		}

		// Token: 0x04000072 RID: 114
		private SubsystemTime m_subsystemTime;

		// Token: 0x04000073 RID: 115
		private SubsystemPlayers m_subsystemPlayers;

		// Token: 0x04000074 RID: 116
		private SubsystemModelsRenderer m_subsystemModelsRenderer;

		// Token: 0x04000075 RID: 117
		private ComponentBody m_componentBody;

		// Token: 0x04000076 RID: 118
		private BitmapFont m_font;

		// Token: 0x04000077 RID: 119
		private Random m_random = new Random();

		// Token: 0x04000078 RID: 120
		private ComponentRandomChatter2.State m_currentState;

		// Token: 0x04000079 RID: 121
		private string m_activePhrase;

		// Token: 0x0400007A RID: 122
		private double m_riseStartTime;

		// Token: 0x0400007B RID: 123
		private double m_fadeStartTime;

		// Token: 0x0400007C RID: 124
		private double m_nextChatterTime;

		// Token: 0x0400007D RID: 125
		private static readonly List<string> m_phrases = new List<string>
		{
			"Alianza es una mierda\r\nMerece ser exterminada",
			"A la mierda Alianza",
			"Los Digimons merecen ser exterminados",
			"Viva Muerte\r\nAbajo Alianza",
			"Oh yeah, fuck, I'm coming!",
			"Yeah Baby",
			"Viva el señor Mencho",
			"El problema es que somos demasiados",
			"Muerte ya esta aquí",
			"El infierno morado es el mejor paraíso",
			"Te vamos a follar zorra",
			"Somos tus machos a joderte la vida she",
			"Fuck, esto es lo mejor",
			"Oh yeah baby",
			"Chinga tu madre, motherfucker",
			"Son a bitch",
			"Yo te boté\r\nTe di banda y te solté, yo te solté\r\nPa'l carajo te mandé, yo te mandé\r\nY a tu amiga me clavé, me la clavé\r\nFuck you, hijueputa, yeh",
			"Vete a la VRG",
			"Criminal, cri-criminal\r\nTu estilo, tu flow, mami, muy criminal"
		};

		// Token: 0x02000011 RID: 17
		[NullableContext(0)]
		private enum State
		{
			// Token: 0x040000AB RID: 171
			Inactive,
			// Token: 0x040000AC RID: 172
			Rising,
			// Token: 0x040000AD RID: 173
			FadeOut
		}
	}
}
