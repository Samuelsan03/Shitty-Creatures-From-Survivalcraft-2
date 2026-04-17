using System;
using System.Runtime.CompilerServices;
using Engine;
using Engine.Graphics;

namespace Game
{
	// Token: 0x02000019 RID: 25
	public class SniperScopeCamera : BasePerspectiveCamera, IScalableCamera
	{
		// Token: 0x1700000E RID: 14
		// (get) Token: 0x06000089 RID: 137 RVA: 0x0000737F File Offset: 0x0000557F
		public override bool UsesMovementControls
		{
			get
			{
				return true;
			}
		}

		// Token: 0x1700000F RID: 15
		// (get) Token: 0x0600008A RID: 138 RVA: 0x00007382 File Offset: 0x00005582
		public override bool IsEntityControlEnabled
		{
			get
			{
				return true;
			}
		}

		// Token: 0x0600008B RID: 139 RVA: 0x00007385 File Offset: 0x00005585
		public SniperScopeCamera(GameWidget gameWidget) : base(gameWidget)
		{
		}

		// Token: 0x0600008C RID: 140 RVA: 0x000073B8 File Offset: 0x000055B8
		public override void Activate(Camera previousCamera)
		{
			this.m_cameraAngles.Y = (float)Math.Asin((double)previousCamera.ViewDirection.Y);
			this.m_cameraAngles.X = (float)Math.Acos((double)previousCamera.ViewDirection.X / Math.Cos((double)this.m_cameraAngles.Y));
			bool flag = previousCamera.ViewDirection.Z > 0f;
			if (flag)
			{
				this.m_cameraAngles.X = -this.m_cameraAngles.X;
			}
			base.SetupPerspectiveCamera(previousCamera.ViewPosition, previousCamera.ViewDirection, previousCamera.ViewUp);
		}

		// Token: 0x0600008D RID: 141 RVA: 0x0000745C File Offset: 0x0000565C
		public Vector3 GetDirection()
		{
			return SniperScopeCamera.s_cameraDirection;
		}

		// Token: 0x0600008E RID: 142 RVA: 0x00007474 File Offset: 0x00005674
		public override void Update(float dt)
		{
			ComponentPlayer componentPlayer = base.GameWidget.PlayerData.ComponentPlayer;
			bool flag = componentPlayer == null || base.GameWidget.Target == null;
			if (!flag)
			{
				ComponentInput componentInput = componentPlayer.ComponentInput;
				int activeBlockValue = componentPlayer.ComponentMiner.ActiveBlockValue;
				bool flag2 = Terrain.ExtractContents(activeBlockValue) != BlocksManager.GetBlockIndex(typeof(SniperBlock), true, false);
				if (flag2)
				{
					componentPlayer.GameWidget.ActiveCamera = new FppCamera(componentPlayer.GameWidget);
				}
				else
				{
					Vector2 cameraLook = componentInput.PlayerInput.CameraLook;
					Vector3 cameraMove = componentInput.PlayerInput.CameraMove;
					this.m_cameraAngles.X = MathUtils.NormalizeAngle(this.m_cameraAngles.X - 4f * cameraLook.X * dt);
					this.m_cameraAngles.Y = Math.Clamp(this.m_cameraAngles.Y + 4f * cameraLook.Y * dt, MathUtils.DegToRad(-45f), MathUtils.DegToRad(80f));
					this.m_cameraScale = Math.Clamp(this.m_cameraScale + cameraMove.Z * dt, 0.2f, 5f);
					Vector3 v = Vector3.Transform(new Vector3(0.5f, 0f, 0f), Matrix.CreateFromYawPitchRoll(this.m_cameraAngles.X, 0f, this.m_cameraAngles.Y));
					Vector3 vector = base.GameWidget.Target.ComponentBody.BoundingBox.Center() + new Vector3(0f, 0.5f, 0f);
					this.m_cameraPosition = vector + v;
					SniperScopeCamera.s_cameraDirection = Vector3.Normalize(this.m_cameraPosition - vector);
					bool flag3 = componentPlayer.m_subsystemTerrain.Raycast(this.m_cameraPosition, this.m_cameraPosition + 1f * SniperScopeCamera.s_cameraDirection, false, true, (int value, float distance) => Terrain.ExtractContents(value) != 0) != null;
					if (flag3)
					{
						componentPlayer.GameWidget.ActiveCamera = new FppCamera(componentPlayer.GameWidget);
					}
					else
					{
						base.SetupPerspectiveCamera(this.m_cameraPosition + new Vector3(0f, 0.15f, 0f), SniperScopeCamera.s_cameraDirection, Vector3.UnitY);
					}
				}
			}
		}

		// Token: 0x17000010 RID: 16
		// (get) Token: 0x0600008F RID: 143 RVA: 0x000076EC File Offset: 0x000058EC
		public override Matrix ProjectionMatrix
		{
			get
			{
				Matrix matrix = this.CalculateBaseProjectionMatrix();
				ViewWidget viewWidget = base.GameWidget.ViewWidget;
				bool flag = viewWidget.ScalingRenderTargetSize == null;
				if (flag)
				{
					matrix *= MatrixUtils.CreateScaleTranslation(this.m_cameraScale * viewWidget.ActualSize.X, -this.m_cameraScale * viewWidget.ActualSize.Y, viewWidget.ActualSize.X / 2f, viewWidget.ActualSize.Y / 2f) * viewWidget.GlobalTransform * MatrixUtils.CreateScaleTranslation(2f / (float)Display.Viewport.Width, -2f / (float)Display.Viewport.Height, -1f, 1f);
				}
				return matrix;
			}
		}

		// Token: 0x04000070 RID: 112
		private Vector3 m_cameraPosition;

		// Token: 0x04000071 RID: 113
		private Vector2 m_cameraAngles = new Vector2(0f, MathUtils.DegToRad(30f));

		// Token: 0x04000072 RID: 114
		private float m_cameraScale = 0.3f;

		// Token: 0x04000073 RID: 115
		private static Vector3 s_cameraDirection;
	}
}
