using System.Linq;
using Cysharp.Threading.Tasks;
using Nox.CCK.Properties;
using Nox.CCK.Utils;
using Nox.Controllers;
using Nox.Entities;
using Nox.Players;
using Nox.Sessions;
using Nox.Worlds;
using UnityEngine.Events;

namespace Nox.Offline.Runtime {
	public sealed class Session : BaseEditablePropertyObject, ISession {
		internal Session(string id) {
			_id           = id;
			InterEntities = new Entities(this);
			InterState    = new State(Status.Pending, "Session is initializing", 0f);
		}

		internal          IState     InterState;
		internal          Dimensions InterDimensions;
		internal readonly Entities   InterEntities;
		private readonly  string     _id;

		public string Id
			=> _id;

		public IDimensions Dimensions
			=> InterDimensions;

		public IEntities Entities
			=> InterEntities;


		internal void UpdateState(Status stt, string msg, float pg)
			=> State = new State(stt, msg, pg);

		internal void SetDimension(IRuntimeWorld scene)
			=> InterDimensions = new Dimensions(scene);

		internal string Tag
			=> GetType().Name + $"_{Id}";


		public IState State {
			get => InterState;
			private set {
				InterState = value;
				Logger.LogDebug($"{value.Status}: {value.Message} ({value.Progress:P0})", tag: Tag);
				OnStateChanged.Invoke(value);
			}
		}

		public IPlayer MasterPlayer {
			get => InterEntities.GetEntity<Player>(InterEntities.MasterId);
			set => Logger.LogWarning("Setting the master player is not supported in offline sessions.", tag: Tag);
		}

		public IPlayer LocalPlayer {
			get => InterEntities.GetEntity<Player>(InterEntities.LocalId);
			set => Logger.LogWarning("Setting the local player is not supported in offline sessions.", tag: Tag);
		}

		public async UniTask Dispose() {
			await UniTask.Yield();
			InterEntities.Dispose();
			InterDimensions.Dispose();
		}

		public async UniTask OnSelect(ISession old) {
			Logger.LogDebug("Selecting session", tag: Tag);

			if (InterDimensions == null) {
				Logger.LogDebug("No dimension found", tag: Tag);
				return;
			}

			if (!await InterDimensions.CreateIfMissing(Runtime.Dimensions.MainIndex)) {
				Logger.LogError("Failed to create main scene", tag: Tag);
				return;
			}

			InterDimensions.SetActive(Runtime.Dimensions.MainIndex, true);
			InterDimensions.SetCurrent();


			var player = InterEntities.NewPlayer();
			await UniTask.Yield();

			OnControllerChanged(Main.ControllerAPI.Current);
		}

		public void OnControllerChanged(IController controller) {
			foreach (var player in InterEntities.GetEntities<Player>())
				player.UpdateController(controller);
		}

		public async UniTask OnDeselect(ISession @new) {
			Logger.LogDebug("Deselecting session", tag: Tag);

			if (InterDimensions == null) {
				Logger.LogWarning("The scene has no dimension assigned. Skipping visibility updates.", tag: Tag);
				await UniTask.Yield();
				return;
			}

			InterDimensions.SetActive(Runtime.Dimensions.MainIndex, false);

			await UniTask.Yield();

			foreach (var player in InterEntities.GetEntities<Player>())
				player.RemoveController();
		}

		public UnityEvent<IPlayer> OnPlayerJoined { get; } = new();

		public UnityEvent<IPlayer> OnPlayerLeft { get; } = new();

		public UnityEvent<IPlayer, bool> OnPlayerVisibility { get; } = new();

		public UnityEvent<IEntity> OnEntityRegistered { get; } = new();

		public UnityEvent<IEntity> OnEntityUnregistered { get; } = new();

		public UnityEvent<IState> OnStateChanged { get; } = new();

		public UnityEvent<IPlayer, IPlayer> OnAuthorityTransferred { get; } = new();

		private ISessionModule[] GetAllModules()
			=> InterDimensions.GetDescriptors()
				.Where(e => e != null)
				.SelectMany(d => d.GetModules<ISessionModule>())
				.ToArray();

		public void HandlePlayerJoined(IPlayer player) {
			Logger.LogDebug($"OnPlayerJoined: {player}", tag: Tag);

			// Si c'est un joueur local, vérifier s'il faut le téléporter au spawn
			if (player.IsLocal)
				player.Respawn();

			Main.CoreAPI.EventAPI.Emit("session_player_joined", this, player);
			OnPlayerJoined.Invoke(player);

			foreach (var module in GetAllModules())
				module.OnPlayerJoined(player);

			var master = MasterPlayer;
			if (master != null && player.Id != master.Id) {
				InterEntities.MasterId = player.Id;
				HandleAuthorityTransferred(player, master);
			}
		}

		public void HandlePlayerLeft(IPlayer player) {
			Logger.LogDebug($"OnPlayerLeft: {player}");

			Main.CoreAPI.EventAPI.Emit("session_player_left", this, player);
			OnPlayerLeft.Invoke(player);

			var master = MasterPlayer;
			if (master != null && player.Id == master.Id) {
				var newMaster = InterEntities.GetEntities<Player>()
					.FirstOrDefault(p => p.Id != player.Id);
				InterEntities.MasterId = newMaster?.Id ?? Nox.Offline.Runtime.Entities.InvalidEntityId;
				HandleAuthorityTransferred(newMaster, master);
			}

			foreach (var module in GetAllModules())
				module.OnPlayerLeft(player);
		}

		public void HandleEntityRegistered(IEntity entity) {
			Logger.LogDebug($"OnEntityRegistered: {entity}");

			Main.CoreAPI.EventAPI.Emit("session_entity_registered", this, entity);
			OnEntityRegistered.Invoke(entity);

			foreach (var module in GetAllModules())
				module.OnEntityRegistered(entity);

			if (entity is IPlayer player)
				HandlePlayerJoined(player);
		}

		public void HandleEntityUnregistered(IEntity entity) {
			if (entity is IPlayer player)
				HandlePlayerLeft(player);

			Logger.LogDebug($"Unregistering entity {entity}");

			Main.CoreAPI.EventAPI.Emit("session_entity_unregistered", this, entity);
			OnEntityUnregistered.Invoke(entity);

			foreach (var module in GetAllModules())
				module.OnEntityUnregistered(entity);
		}

		public void HandleAuthorityTransferred(IPlayer player, IPlayer previous) {
			Logger.LogDebug($"OnAuthorityTransferred: {player}");

			Main.CoreAPI.EventAPI.Emit("session_authority_transferred", this, player, previous);
			OnAuthorityTransferred.Invoke(player, previous);

			foreach (var module in GetAllModules())
				module.OnAuthorityTransferred(player, previous);
		}
	}
}