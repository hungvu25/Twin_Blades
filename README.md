# Twin Blades (Unity + Mirror)

Twin Blades la du an game 2D top-down co-op su dung Mirror Networking.
Gameplay chinh: di chuyen, chien dau voi quai Skeleton, nhat loot, nang cap vu khi, chuyen scene qua cua, va hoi sinh khi het mau.

## 1. Tong Quan Tinh Nang

- Multiplayer theo mo hinh server-authoritative (Mirror).
- Nhan vat co di chuyen, chay nhanh, tan cong ky nang va tan cong vu khi.
- Quai co AI: tim nguoi choi gan nhat, truy duoi, tan cong, nhan sat thuong, roi loot khi chet.
- Loot gom do stackable va vu khi random stat/effect.
- Inventory dong bo qua mang (SyncList).
- Chuyen map dong bo toan room qua `ServerChangeScene`.

## 2. Cau Truc Thu Muc Chinh

- `Project/Features/Player`: movement, combat, stats, inventory, network state.
- `Project/Features/Monster`: AI quai, animation, spawner, HP bar.
- `Project/Features/Loot`: loot table, generator, world loot.
- `Project/Features/Items`: item definition + item database.
- `Project/Features/Environment/Props/Door`: tuong tac cua, chuyen scene, fader.

## 3. Dieu Khien Mac Dinh

### Di Chuyen

- `W/A/S/D`: di chuyen.
- `Left Shift` hoac `Right Shift`: chay nhanh.

### Chien Dau

- `Q`: normal skill attack.
- `E` (giu >= 0.5s): charge attack.
- `R`: full attack.
- `J` hoac `Left Mouse`: tan cong vu khi dang equip.
- `1/2/3`: doi weapon slot.

### Tuong Tac

- `F`: nhat loot gan nhat trong tam.
- `E`: tuong tac cua de chuyen scene (khi dang o trigger).

### Debug Inventory (neu dang bat)

- `F6`: add item test.
- `F7`: use item test.
- `F8`: remove item test.
- `F9`: in inventory local ra Console.

## 4. Luong Choi Co Ban

1. Host va client vao game.
2. Player spawn vao map.
3. Di chuyen, gap quai trong vung spawn.
4. Tieu diet quai de roi loot.
5. Nhat loot de vao inventory/weapon inventory.
6. Qua cua de chuyen scene cho ca room.
7. Khi chet: player hoi sinh sau khoang tre (mac dinh 2s).

## 5. Networking (Mirror)

- Script quan ly network manager: `Project/Features/Player/Scripts/CustomNetworkManager.cs`.
- Dong bo state nhan vat (move/run/alive/facing): `PlayerNetworkState`.
- Dong bo inventory:
  - Stackable: `NetworkInventory` (SyncList InventoryEntry).
  - Vu khi unique: `PlayerWeaponInventory` (SyncList WeaponInstanceData).
- AI quai chay tren server; sat thuong va loot xu ly server-side.

## 6. Setup Bat Buoc Trong Unity

### 6.1 Scene Build Settings

Dam bao scene can dung da duoc them vao Build Settings, toi thieu:

- `BattleScene`
- `Scenes/SampleScene`

Neu scene khong co trong Build Settings, `ServerChangeScene` se that bai.

### 6.2 Tag va Layer

Can co cac tag/layer duoc scripts su dung:

- Tag: `Player`, `BaseSpawn`.
- Layer: nen co layer rieng cho Player/Monster de hit detection ro rang.

### 6.3 Player Prefab

Player prefab nen co day du:

- `NetworkIdentity`
- `Rigidbody2D`
- `PlayerMovement`
- `PlayerNetworkState`
- `PlayerStats`
- `CombatInputController`
- `PlayerWeaponCombat`
- `NetworkInventory`
- `PlayerWeaponInventory`
- `PlayerLootInteractor`
- `PlayerController`

### 6.4 Monster Prefab

Monster prefab nen co:

- `NetworkIdentity`
- `Rigidbody2D`
- `Collider2D`
- `MonsterAI`
- `MonsterAnimationGeneric`
- `MonsterHealthBarUI`
- `MonsterLootTable` (asset tham chieu)
- `WorldLoot` prefab tham chieu trong `MonsterAI`

### 6.5 Item Database

Tao asset ItemDatabase va dat dung duong dan resource neu ban dung tu dong load:

- Resource path mac dinh: `Resources/ItemDatabase`

## 7. Loot va Vu Khi Random

He thong loot sinh tu `MonsterLootTable`:

- Drop stackable: theo `dropChance`, `minAmount`, `maxAmount`.
- Drop vu khi: random bonus attack, attack speed %, effect chance.
- Weapon instance co `instanceId` rieng (GUID) de tranh trung lap.

## 8. Cac Script Quan Trong

- Player movement: `Project/Features/Player/Scripts/PlayerMovement.cs`
- Skill input/combo: `Project/Features/Player/Scripts/CombatInputController.cs`
- Weapon combat: `Project/Features/Player/Scripts/PlayerWeaponCombat.cs`
- Player HP/respawn: `Project/Features/Player/Scripts/PlayerController.cs`
- Monster AI/combat/death: `Project/Features/Monster/Skeleton/Script/MonsterAI.cs`
- Monster spawn: `Project/Features/Monster/Skeleton/Script/MonsterSpawner.cs`
- Loot generator: `Project/Features/Loot/Scripts/LootGenerator.cs`
- Door scene change: `Project/Features/Environment/Props/Door/Scripts/DoorTrigger.cs`

## 9. Troubleshooting Nhanh

- Khong chuyen scene duoc:
  - Kiem tra scene name trong `DoorTrigger.nextSceneName`.
  - Kiem tra scene da them vao Build Settings.
  - Kiem tra server dang active.

- Quai khong tan cong:
  - Kiem tra `attackRange`, `attackCooldown`, animation event.
  - Kiem tra player co tag `Player`.

- Khong nhat duoc loot:
  - Kiem tra layer mask trong `PlayerLootInteractor`.
  - Kiem tra loot co `NetworkIdentity`.

- Khong roi loot:
  - Kiem tra `lootTable`, `worldLootPrefab`, va cac `dropChance`.

## 10. Goi Y Mo Rong

- Them UI inventory thuc te (drag/drop, tooltip, equip compare).
- Tach Input sang Input Actions asset thay vi doc truc tiep Keyboard.
- Them save/load data nhan vat va progression.
- Toi uu tim target cua quai bang bo loc theo vung thay vi quet tat ca Player.
