using System;
using System.Collections.Generic;
using Engine;
using Game;

namespace Game
{
	internal class RefrigeratorXiaomiElectricElement : ElectricElement
	{
		public RefrigeratorXiaomiElectricElement(SubsystemElectricity subsystemElectricity, Point3 point) :
			base(subsystemElectricity, new List<CellFace>
			{
				new CellFace(point.X, point.Y, point.Z, 0),
				new CellFace(point.X, point.Y, point.Z, 1),
				new CellFace(point.X, point.Y, point.Z, 2),
				new CellFace(point.X, point.Y, point.Z, 3),
				new CellFace(point.X, point.Y, point.Z, 4),
				new CellFace(point.X, point.Y, point.Z, 5)
			})
		{
			this.m_subsystemBlockEntities = base.SubsystemElectricity.Project.FindSubsystem<SubsystemBlockEntities>(true);
		}

		public override bool Simulate()
		{
			bool hasPower = base.CalculateHighInputsCount() > 0;

			if (this.m_subsystemBlockEntities != null)
			{
				ComponentBlockEntity blockEntity = this.m_subsystemBlockEntities.GetBlockEntity(
					base.CellFaces[0].Point.X,
					base.CellFaces[0].Point.Y,
					base.CellFaces[0].Point.Z);

				if (blockEntity != null)
				{
					ComponentRefrigeratorXiaomi componentRefrigeratorXiaomi = blockEntity.Entity.FindComponent<ComponentRefrigeratorXiaomi>();
					if (componentRefrigeratorXiaomi != null)
					{
						componentRefrigeratorXiaomi.Freeze(hasPower);
					}
				}
			}

			return false;
		}

		public SubsystemBlockEntities m_subsystemBlockEntities;
	}
}
