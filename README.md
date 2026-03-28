# Twin Blades (Unity + Mirror)

Twin Blades là game 2D top-down co-op sử dụng **Unity** và **Mirror Networking**.  
Gameplay tập trung vào di chuyển, chiến đấu với quái Skeleton, nhặt loot, nâng cấp vũ khí, chuyển scene qua cửa, và hồi sinh sau khi hết máu.

---

## Gameplay Preview

![Gameplay - Map va UI](https://github.com/user-attachments/assets/33612639-02c6-4b41-ab95-7cdb1f414ec3)

![Gameplay - Combat voi quai](https://github.com/user-attachments/assets/7125f1ae-228a-4f30-874c-8ea78b6b0191)

![Gameplay - Loot sau khi ha quai](https://github.com/user-attachments/assets/eaafb126-90c1-45eb-8e81-4e5d03b61562)

---

## Noi Bat

- Multiplayer theo mô hình **server-authoritative** với Mirror.
- Nhân vật có thể **di chuyển, chạy nhanh, dùng skill và tấn công bằng vũ khí**.
- Quái có AI: **tìm người chơi gần nhất, truy đuổi, tấn công, nhận sát thương và rơi loot khi chết**.
- Loot gồm:
  - **Stackable items**
  - **Vũ khí random stat / effect**
- Inventory được **đồng bộ qua mạng bằng SyncList**.
- Chuyển map đồng bộ toàn room qua `ServerChangeScene`.

---

## Muc Luc

1. [Tong Quan Tinh Nang](#1-tong-quan-tinh-nang)  
2. [Cau Truc Thu Muc Chinh](#2-cau-truc-thu-muc-chinh)  
3. [Dieu Khien Mac Dinh](#3-dieu-khien-mac-dinh)  
4. [Luong Choi Co Ban](#4-luong-choi-co-ban)  
5. [Networking (Mirror)](#5-networking-mirror)  
6. [Setup Bat Buoc Trong Unity](#6-setup-bat-buoc-trong-unity)  
7. [Loot va Vu Khi Random](#7-loot-va-vu-khi-random)  

---

## 1. Tong Quan Tinh Nang

- Multiplayer theo mô hình server-authoritative (Mirror).
- Nhân vật có di chuyển, chạy nhanh, tấn công kỹ năng và tấn công vũ khí.
- Quái có AI: tìm người chơi gần nhất, truy đuổi, tấn công, nhận sát thương, rơi loot khi chết.
- Loot gồm đồ stackable và vũ khí random stat / effect.
- Inventory đồng bộ qua mạng bằng `SyncList`.
- Chuyển map đồng bộ toàn room qua `ServerChangeScene`.

## 2. Cau Truc Thu Muc Chinh

- `Project/Features/Player`: movement, combat, stats, inventory, network state.
- `Project/Features/Monster`: AI quái, animation, spawner, HP bar.
- `Project/Features/Loot`: loot table, generator, world loot.
- `Project/Features/Items`: item definition và item database.
- `Project/Features/Environment/Props/Door`: tương tác cửa, chuyển scene, fader.

## 3. Dieu Khien Mac Dinh

### Di Chuyen

- `W / A / S / D`: di chuyển
- `Left Shift` hoặc `Right Shift`: chạy nhanh

### Chien Dau

- `Space`: normal skill attack
- `E` (giữ >= 0.2s): charge attack
- `R`: full attack
- `J` hoặc `Left Mouse`: tấn công bằng vũ khí đang equip

### Tuong Tac

- `F`: nhặt loot gần nhất trong tầm
- `E`: tương tác cửa để chuyển scene (khi đang ở trigger)

### Debug Inventory (neu dang bat)

- `F6`: add item test
- `F7`: use item test
- `F8`: remove item test
- `F9`: in inventory local ra Console

## 4. Luong Choi Co Ban

1. Host và client vào game.
2. Player spawn vào map.
3. Di chuyển và gặp quái trong vùng spawn.
4. Tiêu diệt quái để rơi loot.
5. Nhặt loot để đưa vào inventory / weapon inventory.
6. Qua cửa để chuyển scene cho cả room.
7. Khi chết, player hồi sinh sau khoảng trễ mặc định `2s`.

## 5. Networking (Mirror)

- Script quản lý network manager: `Project/Features/Player/Scripts/CustomNetworkManager.cs`
- Đồng bộ state nhân vật (move / run / alive / facing): `PlayerNetworkState`
- Đồng bộ inventory:
  - Stackable: `NetworkInventory` (`SyncList<InventoryEntry>`)
  - Vũ khí unique: `PlayerWeaponInventory` (`SyncList<WeaponInstanceData>`)
- AI quái chạy trên server; sát thương và loot được xử lý server-side.

## 6. Setup Bat Buoc Trong Unity

### 6.1 Scene Build Settings

Đảm bảo các scene cần dùng đã được thêm vào **Build Settings**, tối thiểu:

- `BattleScene`
- `Scenes/SampleScene`

Nếu scene không có trong Build Settings, `ServerChangeScene` sẽ thất bại.

### 6.2 Tag va Layer

Cần có các tag / layer được script sử dụng:

- Tag: `Player`, `BaseSpawn`
- Layer: nên có layer riêng cho `Player` và `Monster` để hit detection rõ ràng

### 6.3 Player Prefab

Player prefab nên có đầy đủ:

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

Monster prefab nên có:

- `NetworkIdentity`
- `Rigidbody2D`
- `Collider2D`
- `MonsterAI`
- `MonsterAnimationGeneric`
- `MonsterHealthBarUI`
- `MonsterLootTable` (asset tham chiếu)
- `WorldLoot` prefab tham chiếu trong `MonsterAI`

### 6.5 Item Database

Tạo asset `ItemDatabase` và đặt đúng đường dẫn resource nếu dùng tự động load:

- Default resource path: `Resources/ItemDatabase`

## 7. Loot va Vu Khi Random

Hệ thống loot sinh từ `MonsterLootTable`:

- Drop stackable theo:
  - `dropChance`
  - `minAmount`
  - `maxAmount`
- Drop vũ khí với chỉ số random:
  - bonus attack
  - attack speed %
  - effect chance
- Mỗi weapon instance có `instanceId` riêng (`GUID`) để tránh trùng lặp
