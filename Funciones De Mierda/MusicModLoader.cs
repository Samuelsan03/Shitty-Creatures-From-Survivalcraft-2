using System;
using Engine;
using Engine.Audio;
using Engine.Media;
using Game;

namespace Game
{
	public class MusicModLoader : ModLoader
	{
		// Lista de pistas de música disponibles
		private static readonly string[] musicTracks = new string[]
		{
			"MenuMusic/Digimon 02 Target Wada Kouji",
			"MenuMusic/Touhou 2 Mimas Theme Complete Darkness",
			"MenuMusic/Touhou 2 Eastern Wind",
			"MenuMusic/Touhou 2 Record of the Sealing of an Oriental Demon",
			"MenuMusic/Digimon 02 Evolution Break Up Ayumi Miyazaki",
			"MenuMusic/Digimon Adventure 01 Brave Heart Wada Kouji",
			"MenuMusic/Digimon Adventure 01 Butterfly Wada Kouji",
			"MenuMusic/Digimon Savers OP1 Theme Song Gouing Going My Soul Dynamite SHU",
			"MenuMusic/Digimon Savers OP2 Hirari Wada Kouji",
			"MenuMusic/EoSD Credits Theme Crimson Belvedere Eastern Dream",
			"MenuMusic/Digimon Tamers The Biggest Dreamer Wada Kouji",
			"MenuMusic/Touhou 6 Flandre Scarlets Theme U.N. Owen was her",
			"MenuMusic/Digimon Frontiers FIRE Wada Kouji",
			"MenuMusic/Rocket Knight Adventures Stage 1-1",
			"MenuMusic/Rocket Knight Adventures Stage 1-2",
			"MenuMusic/Sparkster (SEGA Genesis) Stage 1-1",
			"MenuMusic/Sparkster (SNES) Stage Lakeside",
			"MenuMusic/Space Harrier Theme",
			"MenuMusic/MAGICAL SOUND SHOWER OutRun",
			"MenuMusic/Super Hang-On Outride A Crisis",
			"MenuMusic/Super Hang-On Sprinter",
			"MenuMusic/Super Hang-On Winning Run",
			"MenuMusic/Nichijou Koigokoro Wa Dangan Mo Yawarakakusuru",
			"MenuMusic/SEGA Mega CD Japanese European Gamerip BIOS",
			"MenuMusic/SEGA CD American BIOS Gamerip Version 01",
			"MenuMusic/SEGA CD American BIOS Gamerip Version 02",
			"MenuMusic/Sonic The Hedgehog 1991 Spring Yard Zone",
			"MenuMusic/Sonic The Hedgehog 1991 Marble Zone",
			"MenuMusic/Sonic The Hedgehog 2 1992 Hill Top Zone",
			"MenuMusic/Mio Honda 本田未央 Step! ステップ",
			"MenuMusic/Yahpp Sorceress Elise",
			"MenuMusic/Chrono Trigger Main Theme",
			"MenuMusic/Twill STAND UP Digimon Xros Wars Hunters",
			"MenuMusic/Sonar Pocket Never Give Up! Digimon Fusion",
			"MenuMusic/Prince Of Persia (SNES) Recap",
			"MenuMusic/Prince Of Persia (SNES) Staff Roll",
			"MenuMusic/FIELD OF VIEW 渇いた叫び - 捨てられた物。",
			"MenuMusic/Mappy 1983 In Game Theme",
			"MenuMusic/Power Rangers The Movie Title Theme SNES",
			"MenuMusic/Sonic Boom Closing Theme Sonic CD",
			"MenuMusic/Sonic Boom Sonic CD",
			"MenuMusic/You Can Do Anything Sonic CD",
		};

		// Variables para control de selección
		private static string lastSelectedTrack = "";
		private static Random random = new Random();
		private static int lastIndex = -1;
		private static bool initialized = false;

		public override void __ModInitialize()
		{
			if (!initialized)
			{
				ModsManager.RegisterHook("MenuPlayMusic", this);

				// Registrar método para actualizar el fade out
				RegisterUpdateHook();

				initialized = true;
			}
		}

		private void RegisterUpdateHook()
		{
			// Podemos usar un hook de actualización si el sistema lo soporta
			// O usar un método estático que sea llamado desde el NewMusicManager
			// Por ahora, confiamos en que NewMusicManager maneje el fade out
		}

		public override void MenuPlayMusic(out string contentMusicPath)
		{
			if (musicTracks.Length == 0)
			{
				contentMusicPath = string.Empty;
				return;
			}

			// Selección aleatoria con semilla basada en tiempo
			int currentTick = Environment.TickCount;
			int index = Math.Abs(currentTick) % musicTracks.Length;

			// Si solo hay una pista, usar esa
			if (musicTracks.Length == 1)
			{
				contentMusicPath = musicTracks[0];
				lastSelectedTrack = contentMusicPath;
				return;
			}

			// Evitar repetir la misma canción consecutivamente
			if (musicTracks[index] == lastSelectedTrack)
			{
				// Buscar siguiente pista diferente
				for (int i = 1; i < musicTracks.Length; i++)
				{
					int nextIndex = (index + i) % musicTracks.Length;
					if (musicTracks[nextIndex] != lastSelectedTrack)
					{
						index = nextIndex;
						break;
					}
				}
			}

			// Si después de buscar aún es la misma (todas iguales o solo hay una diferente),
			// usar selección aleatoria simple
			if (musicTracks[index] == lastSelectedTrack)
			{
				index = random.Int(0, musicTracks.Length - 1);
			}

			string selectedTrack = musicTracks[index];
			lastSelectedTrack = selectedTrack;
			lastIndex = index;

			contentMusicPath = selectedTrack;

		}

		// Método auxiliar para obtener información sobre las pistas
		public static int GetTrackCount()
		{
			return musicTracks.Length;
		}

		public static string[] GetAllTracks()
		{
			return (string[])musicTracks.Clone();
		}

		public static string GetTrackInfo(int index)
		{
			if (index >= 0 && index < musicTracks.Length)
			{
				return musicTracks[index];
			}
			return "Invalid track index";
		}

		// Método para manejar el fade out desde el MusicManager
		public static float GetFadeOutVolume(double currentTime, double trackStartTime)
		{
			const float TRACK_DURATION = 45f;
			const float FADE_START = 40f;
			const float FADE_DURATION = TRACK_DURATION - FADE_START;

			float elapsedSeconds = (float)(currentTime - trackStartTime);

			if (elapsedSeconds < FADE_START)
			{
				// Volumen completo antes del fade
				return 1f;
			}
			else if (elapsedSeconds < TRACK_DURATION)
			{
				// Durante el fade out
				float fadeProgress = (elapsedSeconds - FADE_START) / FADE_DURATION;
				return MathUtils.Max(0f, 1f - fadeProgress);
			}
			else
			{
				// Después de terminar
				return 0f;
			}
		}

		// Método para verificar si es tiempo de cambiar de canción
		public static bool ShouldChangeTrack(double currentTime, double trackStartTime)
		{
			const float TRACK_DURATION = 45f;
			return (currentTime - trackStartTime) >= TRACK_DURATION;
		}

		// Método para obtener una pista específica (útil para testing)
		public static string GetTrackByIndex(int index)
		{
			if (index >= 0 && index < musicTracks.Length)
			{
				return musicTracks[index];
			}
			return string.Empty;
		}

		// Método para obtener una pista diferente a la actual
		public static string GetDifferentTrack(string currentTrack)
		{
			if (musicTracks.Length <= 1)
				return currentTrack;

			int attempts = 0;
			while (attempts < 10) // Límite de intentos para evitar bucle infinito
			{
				int index = random.Int(0, musicTracks.Length - 1);
				if (musicTracks[index] != currentTrack)
				{
					return musicTracks[index];
				}
				attempts++;
			}

			// Si no encuentra diferente después de 10 intentos, usar la siguiente
			for (int i = 0; i < musicTracks.Length; i++)
			{
				if (musicTracks[i] != currentTrack)
				{
					return musicTracks[i];
				}
			}

			// Si todas son iguales, devolver la primera
			return musicTracks[0];
		}
	}
}
