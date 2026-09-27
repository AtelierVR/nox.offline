using UnityEngine;
using Nox.Audio.Players;

namespace Nox.Offline.Runtime {
	/// <summary>
	/// Voice binding of the offline player (<see cref="IPlayerVoice"/>).
	/// <para>
	/// The offline player is the local one, so it also implements <see cref="ILocalPlayerVoice"/>:
	/// the controller's microphone connector binds its live <see cref="ICapturedAudio"/> to
	/// <see cref="Audio"/>, exactly like it does for the relay local player.
	/// </para>
	/// </summary>
	public partial class Player {
		/// <inheritdoc/>
		public ListenMode Listen { get; set; } = ListenMode.Normal;

		/// <inheritdoc/>
		public SpeakMode Speak { get; set; } = SpeakMode.Normal;

		/// <summary>Live captured audio (the microphone), bound by the controller's microphone connector.</summary>
		private ICapturedAudio _audio;

		/// <inheritdoc/>
		public ICapturedAudio Audio {
			get => _audio;
			set => _audio = value;
		}

		/// <summary>Speaking indicator for UI, updated by the voice provider.</summary>
		public bool IsSpeaking { get; set; }

		/// <inheritdoc/>
		public LevelFlags Level {
			get {
				if (!IsSpeaking) return LevelFlags.None;
				return LevelFlags.Speaking | Speak switch {
					SpeakMode.Whisper   => LevelFlags.Whisper,
					SpeakMode.Broadcast => LevelFlags.Broadcast,
					_                   => LevelFlags.Normal
				};
			}
		}

		private float _volume = 1f;

		/// <inheritdoc/>
		public float Volume {
			get => _volume;
			set => _volume = Mathf.Clamp(value, 0f, 2f);
		}

		/// <inheritdoc/>
		public bool IsMuted { get; set; }

		/// <summary>No voice channel offline: the effective values are the local ones.</summary>
		public float EffectiveVolume
			=> Volume;

		/// <inheritdoc cref="EffectiveVolume"/>
		public bool IsEffectivelyMuted
			=> IsMuted;
	}
}
