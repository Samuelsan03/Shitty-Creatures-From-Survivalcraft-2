using System;
using Engine;

namespace Game
{
	// Interfaz para entidades que se sienten atraídas por ruidos (señuelos, comida, etc.)
	public interface INoiseAttractListener
	{
		// Se ejecuta cuando la entidad detecta un ruido atrayente
		void AttractedToNoise(ComponentBody sourceBody, Vector3 sourcePosition, float lureStrength);
	}
}
