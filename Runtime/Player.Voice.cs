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
				if (wasMuted == IsSelfMuted)
					return;
				OnSelfMuteChanged.Invoke(IsSelfMuted);
				PushNameplateStatus();
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
					SpeakMode.Whisper => LevelFlags.Whisper,
					SpeakMode.Broadcast => LevelFlags.Broadcast,
					_ => LevelFlags.Normal
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

		/// <summary>
		/// Voice channel of the session, taken when the voice is bound (see <see cref="ResolveVoiceChannel"/>):
		/// the offline mod registered it at start-up, so it never has to be looked up again.
		/// </summary>
		private IChannelAudio _voiceChannel;

		/// <summary>
		/// Deafen state of that channel (muted, muted by a parent, or at volume 0). Refreshed when the channel
		/// announces a change (see <see cref="OnVoiceChannelMuteChanged"/>), never read from the configuration:
		/// <see cref="IsDeafened"/> is polled every frame by the nameplate.
		/// </summary>
		private bool _deafened;

		/// <summary>Deafen is the voice channel being silent (muted, muted by a parent, or at volume 0).</summary>
		public bool IsDeafened
			=> _deafened;

		/// <summary>
		/// Takes the voice channel the offline mod registered at start-up (<see cref="Main.VoiceRegister"/>): the
		/// player never resolves it through the audio API, which walked the mod list on every read.
		/// </summary>
		private void ResolveVoiceChannel()
			=> _voiceChannel = Main.VoiceRegister?.Channel;

		private readonly NoxEvent<bool> _onSelfMuteChanged = new();
		private readonly NoxEvent<bool> _onDeafenChanged = new();

		/// <inheritdoc />
		public NoxEvent<bool> OnSelfMuteChanged
			=> _onSelfMuteChanged;

		/// <inheritdoc />
		public NoxEvent<bool> OnDeafenChanged
			=> _onDeafenChanged;

		private readonly List<EventSubscription> _voiceSubscriptions = new();

		/// <summary>Device currently capturing for this player, or null while none is bound.</summary>
		private IMicrophone _microphone;

		private VoiceModeState _modeState = new() {
			Desired = SpeakMode.Normal,
			AllowedModes = VoiceModeSettings.ALL_SPEAK_MODES,
		};

		/// <inheritdoc/>
		public SpeakMode DesiredSpeak
			=> _modeState.Desired;

		/// <summary>Set once the "no microphone" warning was emitted, to keep the log quiet.</summary>
		private bool _microphoneWarned;

		/// <summary>
		/// Binds the audio mod's current device, falling back to its default one. Called when the voice
		/// is bound and again on every device event: a device that appears or changes later (audio mod
		/// initialized after us, hot-plug, manual switch) is bound without any per-frame polling.
		/// </summary>
		private void BindCurrentMicrophone() {
			var api = Main.MicrophoneAPI;
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
			Audio = clip ? new CapturedMicrophone(clip, microphone) : null;

			// The device may already be muted (or at volume 0): announce its state right away.
			ApplyMicrophoneSilence();
		}

		/// <summary>Stops the bound device and forgets the capture.</summary>
		private void DetachMicrophone() {
			_microphone?.Stop(MicrophoneOwner);
			_microphone = null;
			Audio = null;
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

		private void OnNextSpeakMode(EventData context) {
			var next = Next(_modeState.Desired);

			if (!VoiceModeSettings.IsAllowed(next, _modeState.AllowedModes))
				next = SpeakMode.Normal;

			SetDesiredSpeak(next);
		}

		private static SpeakMode Next(SpeakMode mode) {
			if (mode is < SpeakMode.Whisper or > SpeakMode.Broadcast)
				return SpeakMode.Normal;

			return mode == SpeakMode.Broadcast
				? SpeakMode.Whisper
				: (SpeakMode)((int)mode + 1);
		}

		private void OnSessionDataChanged(string[] key, object value, object previous) {
			if (key is not { Length: > 0 })
				return;

			var name = string.Join(".", key);
			if (name != VoiceModeSettings.SPEAK_MODE_KEY
				&& name != VoiceModeSettings.ALLOWED_MODES_KEY
				&& name != VoiceModeSettings.FORCE_MUTE_KEY)
				return;

			RefreshSessionVoice();
		}

		private void RefreshSessionVoice() {
			var data = Context.Context?.Data;

			_modeState.ForceMute = data?.Get(VoiceModeSettings.FORCE_MUTE_KEY, false) ?? false;
			_modeState.AllowedModes = data?.Get(VoiceModeSettings.ALLOWED_MODES_KEY, VoiceModeSettings.ALL_SPEAK_MODES) ?? VoiceModeSettings.ALL_SPEAK_MODES;
			_modeState.Session = data != null && data.Has(VoiceModeSettings.SPEAK_MODE_KEY)
				? VoiceModeSettings.ParseMode(data.Get<object>(VoiceModeSettings.SPEAK_MODE_KEY))
				: null;

			SetDesiredSpeak(VoiceModeSettings.Resolve(_modeState.Session, VoiceModeSettings.ChangeOnJoin, _modeState.AllowedModes));
		}

		private void SetDesiredSpeak(SpeakMode mode) {
			_modeState.Desired = mode;
			ApplySpeakState();
			PushNameplateStatus();

			if (_modeState.Announced == mode)
				return;

			_modeState.Announced = mode;
			Main.CoreAPI?.EventAPI.Emit("voice.speak_mode_changed", this, mode);
		}

		private void ApplySpeakState() {
			var muted = _modeState.ForceMute || _microphone == null || _microphone.IsSilent();
			var wanted = muted
				? SpeakMode.Muted
				: _modeState.Desired;

			Speak = wanted;
		}

		/// <summary>Announces whether the bound device is silent (<see cref="MicrophoneExtensions.IsSilent"/>).</summary>
		private void ApplyMicrophoneSilence() {
			if (_microphone == null)
				return;

			ApplySpeakState();
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

			ResolveVoiceChannel();
			_deafened = _voiceChannel.IsSilent();

			if (Main.CoreAPI == null)
				return;

			Main.VoiceRegister?.OnMute.AddListener(OnVoiceChannelMuteChanged);
			Main.VoiceRegister?.OnVolume.AddListener(OnVoiceChannelVolumeChanged);

			_voiceSubscriptions.AddRange(new[] {
				Main.CoreAPI.EventAPI.Subscribe("audio.microphone.added", OnMicrophoneAdded),
				Main.CoreAPI.EventAPI.Subscribe("audio.microphone.current_changed", OnMicrophoneCurrentChanged),
				Main.CoreAPI.EventAPI.Subscribe("audio.microphone.mute_changed", OnMicrophoneMuteChanged),
				Main.CoreAPI.EventAPI.Subscribe("audio.microphone.volume_changed", OnMicrophoneVolumeChanged),
				Main.CoreAPI.EventAPI.Subscribe("audio.microphone.removed", OnMicrophoneRemoved),
				Main.CoreAPI.EventAPI.Subscribe("voice.speak_mode.next", OnNextSpeakMode)
			});

			Context.Context?.Data?.OnChanged.AddListener(OnSessionDataChanged);

			BindCurrentMicrophone();
			RefreshSessionVoice();
		}

		/// <summary>Releases every subscription taken by <see cref="BindVoiceEvents"/>.</summary>
		internal void UnbindVoiceEvents() {
			DetachMicrophone();

			Context.Context?.Data?.OnChanged.RemoveListener(OnSessionDataChanged);

			Main.VoiceRegister?.OnMute.RemoveListener(OnVoiceChannelMuteChanged);
			Main.VoiceRegister?.OnVolume.RemoveListener(OnVoiceChannelVolumeChanged);

			_voiceChannel = null;
			_deafened = false;

			if (_voiceSubscriptions.Count == 0)
				return;

			var core = Main.CoreAPI;
			if (core != null)
				foreach (var subscription in _voiceSubscriptions)
					core.EventAPI.Unsubscribe(subscription);

			_voiceSubscriptions.Clear();
		}

		/// <summary>The voice channel was muted or unmuted (values pushed by <see cref="Main.VoiceRegister"/>).</summary>
		private void OnVoiceChannelMuteChanged(bool muted, bool effectiveMuted)
			=> RefreshDeafened();

		/// <summary>The voice channel volume changed, which is a deafen at 0 (values pushed by the register).</summary>
		private void OnVoiceChannelVolumeChanged(float volume, float effectiveVolume)
			=> RefreshDeafened();

		/// <summary>
		/// Re-reads the deafen state of the channel. Called on the channel events only: the nameplate polls
		/// <see cref="IsDeafened"/> every frame, and the channel state comes from the configuration.
		/// </summary>
		private void RefreshDeafened() {
			var deafened = _voiceChannel.IsSilent();
			if (deafened == _deafened)
				return;

			_deafened = deafened;
			OnDeafenChanged.Invoke(deafened);
			PushNameplateStatus();
		}

		private struct VoiceModeState {
			public SpeakMode Desired;
			public SpeakMode? Announced;
			public SpeakMode? Session;
			public int AllowedModes;
			public bool ForceMute;
		}
	}
}
