using System;
using Engine;
using Engine.Input;

namespace Game
{
	public class FreeCamera : BasePerspectiveCamera
	{
		public override bool UsesMovementControls => true;
		public override bool IsEntityControlEnabled => true;

		private Vector3 m_position;
		private Vector3 m_direction;

		public FreeCamera(GameWidget gameWidget) : base(gameWidget) { }

		public override void Activate(Camera previousCamera)
		{
			m_position = previousCamera.ViewPosition;
			m_direction = previousCamera.ViewDirection;
			SetupPerspectiveCamera(m_position, m_direction, Vector3.UnitY);
		}

		public override void Update(float dt)
		{
			dt = MathUtils.Min(dt, 0.1f);

			Vector3 move = Vector3.Zero;
			Vector2 look = Vector2.Zero;

			ComponentPlayer player = GameWidget.PlayerData?.ComponentPlayer;
			if (player != null)
			{
				PlayerInput input = player.ComponentInput.PlayerInput;
				move = input.CameraMove * new Vector3(1f, 0f, 1f);
				look = input.CameraLook;
			}

			// También permitir teclado directo para mayor flexibilidad
			bool shift = Keyboard.IsKeyDown(Key.Shift);
			bool ctrl = Keyboard.IsKeyDown(Key.Control);

			float speed = 8f;
			if (shift) speed *= 10f;
			if (ctrl) speed /= 10f;

			Vector3 right = Vector3.Normalize(Vector3.Cross(m_direction, Vector3.UnitY));
			Vector3 up = Vector3.UnitY;

			Vector3 totalMove = (move.X * right + move.Y * up + move.Z * m_direction) * speed;

			m_position += totalMove * dt;
			m_direction = Vector3.Transform(m_direction, Matrix.CreateFromAxisAngle(up, -4f * look.X * dt));
			m_direction = Vector3.Transform(m_direction, Matrix.CreateFromAxisAngle(right, 4f * look.Y * dt));

			SetupPerspectiveCamera(m_position, m_direction, Vector3.UnitY);
		}
	}
}
