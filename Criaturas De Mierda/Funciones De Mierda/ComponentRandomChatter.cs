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
	// Token: 0x02000008 RID: 8
	[NullableContext(1)]
	[Nullable(0)]
	public class ComponentRandomChatter : Component, IUpdateable, IDrawable
	{
		// Token: 0x17000006 RID: 6
		// (get) Token: 0x06000042 RID: 66 RVA: 0x00005D58 File Offset: 0x00003F58
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

		// Token: 0x17000007 RID: 7
		// (get) Token: 0x06000043 RID: 67 RVA: 0x00005D78 File Offset: 0x00003F78
		// (set) Token: 0x06000044 RID: 68 RVA: 0x00005D80 File Offset: 0x00003F80
		public float ActivationDistance { get; set; }

		// Token: 0x17000008 RID: 8
		// (get) Token: 0x06000045 RID: 69 RVA: 0x00005D89 File Offset: 0x00003F89
		// (set) Token: 0x06000046 RID: 70 RVA: 0x00005D91 File Offset: 0x00003F91
		public float DisplayDistance { get; set; }

		// Token: 0x17000009 RID: 9
		// (get) Token: 0x06000047 RID: 71 RVA: 0x00005D9A File Offset: 0x00003F9A
		// (set) Token: 0x06000048 RID: 72 RVA: 0x00005DA2 File Offset: 0x00003FA2
		public float MinInterval { get; set; }

		// Token: 0x1700000A RID: 10
		// (get) Token: 0x06000049 RID: 73 RVA: 0x00005DAB File Offset: 0x00003FAB
		// (set) Token: 0x0600004A RID: 74 RVA: 0x00005DB3 File Offset: 0x00003FB3
		public float MaxInterval { get; set; }

		// Token: 0x1700000B RID: 11
		// (get) Token: 0x0600004B RID: 75 RVA: 0x00005DBC File Offset: 0x00003FBC
		// (set) Token: 0x0600004C RID: 76 RVA: 0x00005DC4 File Offset: 0x00003FC4
		public float RiseDuration { get; set; }

		// Token: 0x1700000C RID: 12
		// (get) Token: 0x0600004D RID: 77 RVA: 0x00005DCD File Offset: 0x00003FCD
		// (set) Token: 0x0600004E RID: 78 RVA: 0x00005DD5 File Offset: 0x00003FD5
		public float FadeDuration { get; set; }

		// Token: 0x1700000D RID: 13
		// (get) Token: 0x0600004F RID: 79 RVA: 0x00005DE0 File Offset: 0x00003FE0
		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		// Token: 0x06000050 RID: 80 RVA: 0x00005DF4 File Offset: 0x00003FF4
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

		// Token: 0x06000051 RID: 81 RVA: 0x00005EEC File Offset: 0x000040EC
		public void Update(float dt)
		{
			double gameTime = this.m_subsystemTime.GameTime;
			bool flag = this.m_currentState == ComponentRandomChatter.State.Inactive;
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
						this.m_activePhrase = ComponentRandomChatter.m_phrases[this.m_random.Int(0, ComponentRandomChatter.m_phrases.Count - 1)];
						this.m_currentState = ComponentRandomChatter.State.Rising;
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
				bool flag5 = this.m_currentState == ComponentRandomChatter.State.Rising;
				if (flag5)
				{
					double riseProgress = (gameTime - this.m_riseStartTime) / (double)this.RiseDuration;
					bool flag6 = riseProgress >= 1.0;
					if (flag6)
					{
						this.m_currentState = ComponentRandomChatter.State.FadeOut;
						this.m_fadeStartTime = gameTime;
					}
				}
				else
				{
					bool flag7 = this.m_currentState == ComponentRandomChatter.State.FadeOut;
					if (flag7)
					{
						bool flag8 = gameTime > this.m_fadeStartTime + (double)this.FadeDuration;
						if (flag8)
						{
							this.m_currentState = ComponentRandomChatter.State.Inactive;
							this.m_activePhrase = null;
						}
					}
				}
			}
		}

		// Token: 0x06000052 RID: 82 RVA: 0x000060B8 File Offset: 0x000042B8
		public void Draw(Camera camera, int drawOrder)
		{
			bool flag = this.m_currentState == ComponentRandomChatter.State.Inactive || string.IsNullOrEmpty(this.m_activePhrase);
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
					bool flag4 = this.m_currentState == ComponentRandomChatter.State.FadeOut;
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

		// Token: 0x06000053 RID: 83 RVA: 0x00006370 File Offset: 0x00004570
		private void SetNextChatterTime()
		{
			this.m_nextChatterTime = this.m_subsystemTime.GameTime + (double)this.m_random.Float(this.MinInterval, this.MaxInterval);
		}

		// Token: 0x04000060 RID: 96
		private SubsystemTime m_subsystemTime;

		// Token: 0x04000061 RID: 97
		private SubsystemPlayers m_subsystemPlayers;

		// Token: 0x04000062 RID: 98
		private SubsystemModelsRenderer m_subsystemModelsRenderer;

		// Token: 0x04000063 RID: 99
		private ComponentBody m_componentBody;

		// Token: 0x04000064 RID: 100
		private BitmapFont m_font;

		// Token: 0x04000065 RID: 101
		private Random m_random = new Random();

		// Token: 0x04000066 RID: 102
		private ComponentRandomChatter.State m_currentState;

		// Token: 0x04000067 RID: 103
		private string m_activePhrase;

		// Token: 0x04000068 RID: 104
		private double m_riseStartTime;

		// Token: 0x04000069 RID: 105
		private double m_fadeStartTime;

		// Token: 0x0400006A RID: 106
		private double m_nextChatterTime;

		// Token: 0x0400006B RID: 107
		private static readonly List<string> m_phrases = new List<string>
		{
			"Hola chamitos, aquí el karajito que le sigue la corriente a moralistas",
			"Tengo hambre we",
			"Chupame la pija",
			"Me empezo a meterme a sus grupos de mierda",
			"Los visitare en la noche",
			"Ay ese benson",
			"Callate la boca pendejo",
			"El problema es que somos demasiados",
			"Ya largate de este directo",
			"Te dome sin condon",
			"La bebecita bebe lean y bebe whisky",
			"Soy guapo, lo sé\r\nLas mujeres se calientan, ya lo sé",
			"Dejenmen en paz!",
			"Aw shit, here we go again",
			"Come on sweetheart",
			"La marihuana para siempre",
			"I shot the sheriff",
			"Estoy en tu cesped Nebbercracker xdxdxdxd",
			"¿Dónde están los que hablan de mí?\r\n¿Dónde están? Por el techo van a salir\r\n¿Dónde están los que hablan de mí?\r\n¿Dónde están? Por el techo van a salirLa quimica no fisica magnifica lirica mistica\r\nla habilidad lenguistica y calidad olimpica\r\nhara que esa nena bella baile\r\nen la casa cuando wiso canteOhh yes,\r\nElla es mi chica de la voz sensual\r\nUna romántica llamada\r\nQue penetra en mi corazón y me hace enamorar.\r\nCon forme con forme... one more time"
		};

		// Token: 0x02000010 RID: 16
		[NullableContext(0)]
		private enum State
		{
			// Token: 0x040000A7 RID: 167
			Inactive,
			// Token: 0x040000A8 RID: 168
			Rising,
			// Token: 0x040000A9 RID: 169
			FadeOut
		}
	}
}
