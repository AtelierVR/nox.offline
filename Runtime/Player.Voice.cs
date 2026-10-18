using System.Collections.Generic;
using UnityEngine;
using Nox.Audio;
using Nox.Audio.Players;
using Nox.CCK.Audio;
using Nox.CCK.Events;
using Nox.CCK.Mods.Events;
using Logger = Nox.CCK.Utils.Logger;

namespace Nox.Offline.Runtime {
	/// <summary>
	/// Voice binding of the offline player (<see cref="IPlayerVoice"/>).
	/// <para>
	/// The offline player is the local one, so it also implements <see cref="ILocalPlayerVoice"/> and
	/// owns its microphone capture: it binds the audio mod's current device, exposes its live
	/// <see cref="ICapturedAudio"/> and follows the device mute (self-mute) and the voice channel
	/// (deafen). Nothing else binds a local player offline.
	/// </para>
	/// </summary>
	public partial class Player {
		/// <summary>Key the device is started/stopped with, so it can be shared with other consumers.</summary>
		private const string MicrophoneOwner = "voice";

		/// <inheritdoc/>
		public ListenMode Listen { get; set; } = ListenMode.Normal;

		private SpeakMode _speak = SpeakMode.Normal;

		/// <inheritdoc/>
		public SpeakMode Speak {
			get => _speak;
			set {
				var wasMuted = IsSelfMuted;
				_speak = value;
				if (wasMuted != IsSelfMuted)
					OnSelfMuteChanged.Invoke(IsSelfMuted);
			}
		}

		/// <summary>Live captured audio (the microphone), bound by <see cref="TickVoice"/>.</summary>
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

		/// <summary>The offline player is muted when its microphone capture is paused.</summary>
		public bool IsSelfMuted
			=> Speak == SpeakMode.Muted;

		/// <summary>Deafen is the voice channel being silent (muted, muted by a parent, or at volume 0).</summary>
		public bool IsDeafened
			=> Main.AudioAPI?.GetChannel("voice")?.IsSilent() ?? false;

		private readonly NoxEvent<bool> _onSelfMuteChanged = new();
		private readonly NoxEvent<bool> _onDeafenChanged  = new();

		/// <inheritdoc />
		public NoxEvent<bool> OnSelfMuteChanged
			=> _onSelfMuteChanged;

		/// <inheritdoc />
		public NoxEvent<bool> OnDeafenChanged
			=> _onDeafenChanged;

		private readonly List<EventSubscription> _voiceSubscriptions = new();
		private bool                             _lastDeafened;

		/// <summary>Device currently capturing for this player, or null while none is bound.</summary>
		private IMicrophone _microphone;

		/// <summary>Mode announced while the device is live; a muted device announces <see cref="SpeakMode.Muted"/>.</summary>
		private SpeakMode _speakWhenUnmuted = SpeakMode.Normal;

		/// <summary>Set once the "no microphone" warning was emitted, to keep the log quiet.</summary>
		private bool _microphoneWarned;

		/// <summary>
		/// Binds the audio mod's current device, falling back to its default one. Called when the voice
		/// is bound and again on every device event: a device that appears or changes later (audio mod
		/// initialized after us, hot-plug, manual switch) is bound without any per-frame polling.
		/// </summary>
		private void BindCurrentMicrophone() {
			var api        = Main.MicrophoneAPI;
			var microphone = api?.Current ?? api?.Default;

			if (microphone == null) {
				// The audio mod may not be initialized yet: its next device event binds us.
				if (api != null && !_microphoneWarned) {
					_microphoneWarned = true;
					Logger.LogWarning("No microphone available: the local player stays silent until one is.");
				}
				return;
			}

			_microphoneWarned = false;
			AttachMicrophone(microphone);
		}

		/// <summary>Starts <paramref name="microphone"/> and exposes it as this player's <see cref="Audio"/>.</summary>
		private void AttachMicrophone(IMicrophone microphone) {
			if (_microphone == microphone)
				return;

			DetachMicrophone();

			if (microphone == null)
				return;

			_microphone = microphone;

			var clip = microphone.Start(MicrophoneOwner);
			Audio    = clip ? new CapturedMicrophone(clip, microphone) : null;

			// The device may already be muted (or at volume 0): announce its state right away.
			ApplyMicrophoneSilence();
		}

		/// <summary>Stops the bound device and forgets the capture.</summary>
		private void DetachMicrophone() {
			_microphone?.Stop(MicrophoneOwner);
			_microphone = null;
			Audio       = null;
		}

		/// <summary>
		/// The captured device became silent (muted, or its volume reached 0): announce
		/// <see cref="SpeakMode.Muted"/> and lift it back to the chosen mode when it stops.
		/// </summary>
		private void OnMicrophoneMuteChanged(EventData context) {
			if (!context.TryGet<IMicrophone>(0, out var microphone) || microphone != _microphone)
				return;

			ApplyMicrophoneSilence();
		}

		/// <summary>Volume 0 captures nothing: it is a mute for every consumer of the voice state.</summary>
		private void OnMicrophoneVolumeChanged(EventData context) {
			if (!context.TryGet<IMicrophone>(0, out var microphone) || microphone != _microphone)
				return;

			ApplyMicrophoneSilence();
		}

		/// <summary>Announces whether the bound device is silent (<see cref="MicrophoneExtensions.IsSilent"/>).</summary>
		private void ApplyMicrophoneSilence() {
			if (_microphone == null)
				return;

			if (Speak != SpeakMode.Muted)
				_speakWhenUnmuted = Speak;
			Speak = _microphone.IsSilent() 
				? SpeakMode.Muted 
				: _speakWhenUnmuted;
		}

		/// <summary>A device appeared: bind it while none is bound.</summary>
		private void OnMicrophoneAdded(EventData context) {
			if (_microphone == null)
				BindCurrentMicrophone();
		}

		/// <summary>Rebinds the capture when the audio mod switches device.</summary>
		private void OnMicrophoneCurrentChanged(EventData context)
			=> BindCurrentMicrophone();

		/// <summary>Releases a device that was unplugged, then binds another one if available.</summary>
		private void OnMicrophoneRemoved(EventData context) {
			if (!context.TryGet<IMicrophone>(0, out var microphone) || microphone != _microphone)
				return;

			DetachMicrophone();
			BindCurrentMicrophone();
		}

		/// <summary>
		/// Subscribes the voice events and binds the current device: the microphone is picked up on
		/// load and on every device change, never polled.
		/// </summary>
		internal void BindVoiceEvents() {
			// A rebind must never leave a previous subscription attached.
			UnbindVoiceEvents();

			_lastDeafened = IsDeafened;

			if (Main.CoreAPI == null)
				return;

			_voiceSubscriptions.AddRange(new[] {
				Main.CoreAPI.EventAPI.Subscribe("audio.channel.mute_changed", OnChannelMuteChanged),
				Main.CoreAPI.EventAPI.Subscribe("audio.microphone.added", OnMicrophoneAdded),
				Main.CoreAPI.EventAPI.Subscribe("audio.microphone.current_changed", OnMicrophoneCurrentChanged),
				Main.CoreAPI.EventAPI.Subscribe("audio.microphone.mute_changed", OnMicrophoneMuteChanged),
				Main.CoreAPI.EventAPI.Subscribe("audio.microphone.volume_changed", OnMicrophoneVolumeChanged),
				Main.CoreAPI.EventAPI.Subscribe("audio.microphone.removed", OnMicrophoneRemoved)
			});

			BindCurrentMicrophone();
		}

		/// <summary>Releases every subscription taken by <see cref="BindVoiceEvents"/>.</summary>
		internal void UnbindVoiceEvents() {
			DetachMicrophone();

			if (_voiceSubscriptions.Count == 0)
				return;

			var core = Main.CoreAPI;
			if (core != null)
				foreach (var subscription in _voiceSubscriptions)
					core.EventAPI.Unsubscribe(subscription);

			_voiceSubscriptions.Clear();
		}

		private void OnChannelMuteChanged(EventData context) {
			var deafened = IsDeafened;
			if (deafened == _lastDeafened)
				return;

			_lastDeafened = deafened;
			OnDeafenChanged.Invoke(deafened);
		}
	}
}
