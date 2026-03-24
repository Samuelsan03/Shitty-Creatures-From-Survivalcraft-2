using System;
using System.Collections.Generic;
using Engine;
using Game;

namespace Game
{
	public class MusicModLoader : ModLoader
	{
		// Lista de canciones personalizadas para el menú principal
		private static readonly List<string> _menuSongs = new List<string>
		{
			"MenuMusic/Dragon Quest NES Title Theme",
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
			"MenuMusic/瞬間ときはファンタジー",
			"MenuMusic/Power Rangers The Movie Title Theme SNES",
			"MenuMusic/Sonic Boom Closing Theme Sonic CD",
			"MenuMusic/Sonic Boom Sonic CD",
			"MenuMusic/You Can Do Anything Sonic CD"
		};

		// Generador de números aleatorios para seleccionar canciones
		private static Random _random;

		// Índice de la canción actual para evitar repeticiones consecutivas
		private static int _lastSongIndex = -1;

		/// <summary>
		/// Se ejecuta cuando el cargador del mod es instanciado.
		/// Registra el hook para la música del menú.
		/// </summary>
		public override void __ModInitialize()
		{
			// Inicializar el generador aleatorio
			_random = new Random();

			// Registrar este cargador de mod para el hook "MenuPlayMusic"
			// Prioridad 0 (predeterminado) - números más bajos = mayor prioridad
			ModsManager.RegisterHook("MenuPlayMusic", this);

			// Registrar también el hook para cambiar la música cuando termina una canción
			// Esto permite que el gestor de música reproduzca una nueva canción de nuestra lista
			ModsManager.RegisterHook("PlayInGameMusic", this);

			// Log opcional para verificar que el mod se cargó correctamente
			Log.Information("MusicModLoader: Mod de música personalizada cargado correctamente. " + _menuSongs.Count + " canciones disponibles.");
		}

		/// <summary>
		/// Sobrescribe la música del menú para reproducir una pista personalizada.
		/// Este método se llama cuando se activa el hook "MenuPlayMusic".
		/// </summary>
		/// <param name="contentMusicPath">Ruta de salida con la canción personalizada a reproducir</param>
		public override void MenuPlayMusic(out string contentMusicPath)
		{
			// Seleccionar una canción aleatoria de la lista
			contentMusicPath = GetRandomSong();

			// Log opcional para verificar qué canción se está reproduciendo
			Log.Information("MusicModLoader: Reproduciendo música de menú: " + contentMusicPath);
		}

		/// <summary>
		/// Maneja la reproducción de música durante el juego.
		/// Este método se llama cuando se activa el hook "PlayInGameMusic".
		/// Puedes decidir si quieres modificar la música del juego también.
		/// </summary>
		public override void PlayInGameMusic()
		{
			// Si quieres que también se reproduzca música personalizada durante el juego,
			// descomenta las siguientes líneas:
			/*
            string customMusic = GetRandomSong();
            if (!string.IsNullOrEmpty(customMusic))
            {
                // Nota: La música del juego se maneja de manera diferente
                // Este es un ejemplo de cómo podrías implementarlo
                MusicManager.PlayMusic(customMusic, 0f);
            }
            */

			// Por ahora, no modificamos la música del juego
			Log.Debug("MusicModLoader: Hook PlayInGameMusic ejecutado (sin cambios)");
		}

		/// <summary>
		/// Selecciona una canción aleatoria de la lista, evitando repetir la última canción.
		/// </summary>
		/// <returns>Ruta de la canción seleccionada aleatoriamente</returns>
		private static string GetRandomSong()
		{
			if (_menuSongs.Count == 0)
			{
				Log.Warning("MusicModLoader: No hay canciones disponibles en la lista.");
				return string.Empty;
			}

			// Si solo hay una canción, devolver esa
			if (_menuSongs.Count == 1)
			{
				return _menuSongs[0];
			}

			// Seleccionar un índice aleatorio diferente al último
			int newIndex;
			do
			{
				newIndex = _random.Int(0, _menuSongs.Count - 1);
			}
			while (newIndex == _lastSongIndex);

			// Actualizar el último índice
			_lastSongIndex = newIndex;

			return _menuSongs[newIndex];
		}

		/// <summary>
		/// Opcional: Se ejecuta cuando el mod es descargado.
		/// Limpia los recursos si es necesario.
		/// </summary>
		public override void ModDispose()
		{
			// No es necesario desregistrar hooks manualmente, la API lo maneja automáticamente
			Log.Information("MusicModLoader: Mod descargado correctamente.");
		}
	}
}
