using System.Collections.Generic;
using System.Linq;
using DefaultNamespace;
using Nox.CCK.Players;
using Nox.Controllers;
using Nox.Entities;
using Nox.Players;
using Nox.Users;
using Nox.Worlds.Spawns;
using UnityEngine;
using Logger = Nox.CCK.Utils.Logger;

namespace Nox.Offline.Runtime {
	public class Player : Entity, IPlayer {
		public Player(Entities context, int id) : base(context, id) { }

		private readonly Dictionary<ushort, Part> _parts = new();

		override protected Physical InstantiatePhysical() {
			throw new System.NotImplementedException();
		}

		internal void UpdateController(IController controller) {
			if (controller == null) {
				RemoveController();
				return;
			}

			var cParts = controller.GetParts();

			// remove parts not in controller
			var keysToRemove = _parts.Keys.Except(cParts.Select(p => p.Key)).ToList();
			foreach (var key in keysToRemove)
				_parts.Remove(key);

			// add parts from controller
			foreach (var cPart in cParts) {
				if (_parts.ContainsKey(cPart.Key))
					continue;
				_parts[cPart.Key] = new Part(this, cPart.Key);
			}

			// restore parts
			foreach (var part in _parts.Values)
				part.Restore(controller);
		}

		internal void RemoveController() {
			foreach (var part in _parts.Values)
				part.Store();
		}

		public IPart[] GetParts()
			=> _parts.Values.Cast<IPart>().ToArray();

		public bool TryGetPart(ushort name, out IPart part) {
			if (_parts.TryGetValue(name, out var p)) {
				part = p;
				return true;
			}

			part = null;
			return false;
		}

		public string Display {
			get => Main.UserAPI.GetCurrent()?.GetDisplay() ?? "Local #" + Id;
			set => Logger.LogWarning("Setting Display is not supported in Offline Player.");
		}

		public IUserIdentifier Identifier
			=> Main.UserAPI.GetCurrent()?.ToIdentifier();

		public bool IsMaster
			=> true;

		public bool IsLocal
			=> true;

		public void Teleport(Vector3 position, Quaternion rotation) {
			Position = position;
			Rotation = rotation;
			Velocity = Vector3.zero;
			Angular  = Vector3.zero;
		}

		public void Respawn() {
			if (Context.Context.Dimensions
					.GetDescriptor(Dimensions.MainIndex)
					.GetModules()
					.FirstOrDefault(e => e is ISpawnModule)
				is not ISpawnModule module) {
				Logger.LogWarning("No spawn module found for respawning player.");
				return;
			}

			var spawn = module.ChoiceSpawn();
			Teleport(spawn.Position, spawn.Rotation);
			Logger.Log($"Player {Id} respawned to {spawn.Position}.");
		}

		public Vector3 Position {
			get => _parts.TryGetValue(PlayerRig.Base.ToIndex(), out var p) ? p.Position : Vector3.zero;
			set {
				if (!_parts.TryGetValue(PlayerRig.Base.ToIndex(), out var p))
					return;
				p.Position = value;
			}
		}

		public Quaternion Rotation {
			get => _parts.TryGetValue(PlayerRig.Base.ToIndex(), out var p) ? p.Rotation : Quaternion.identity;
			set {
				if (!_parts.TryGetValue(PlayerRig.Base.ToIndex(), out var p))
					return;
				p.Rotation = value;
			}
		}

		public Vector3 Scale {
			get => _parts.TryGetValue(PlayerRig.Base.ToIndex(), out var p) ? p.Scale : Vector3.one;
			set {
				if (!_parts.TryGetValue(PlayerRig.Base.ToIndex(), out var p))
					return;
				p.Scale = value;
			}
		}

		public Vector3 Velocity {
			get => _parts.TryGetValue(PlayerRig.Base.ToIndex(), out var p) ? p.Velocity : Vector3.zero;
			set {
				if (!_parts.TryGetValue(PlayerRig.Base.ToIndex(), out var p))
					return;
				p.Velocity = value;
			}
		}

		public Vector3 Angular {
			get => _parts.TryGetValue(PlayerRig.Base.ToIndex(), out var p) ? p.Angular : Vector3.zero;
			set {
				if (!_parts.TryGetValue(PlayerRig.Base.ToIndex(), out var p))
					return;
				p.Angular = value;
			}
		}
		
		override protected void OnPhysicalCreated() {
			Context.Context.HandlePlayerVisibilityChanged(this, true);
		}

		override protected void OnPhysicalDestroyed() {
			Context.Context.HandlePlayerVisibilityChanged(this, false);
		}

		public override string ToString()
			=> $"{GetType().Name}[Id={Id}, Display={Display}, Identifier={Identifier}, IsMaster={IsMaster}, IsLocal={IsLocal}]";
	}
}