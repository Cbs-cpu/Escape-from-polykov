using System.Collections.Generic;

namespace Polykov.Inventory
{
    public enum ItemCategory : byte
    {
        Weapon, WeaponPart, Magazine, Ammo, Medical, Food, Headwear, Earpiece, FaceCover, Eyewear, Armband,
        BodyArmor, Rig, Backpack, SecureContainer, Melee, Barter, Key, Money, Container,
    }

    /// <summary>Character equipment slots (Tarkov layout).</summary>
    public enum EquipSlot : byte
    {
        Earpiece, Headwear, FaceCover, Eyewear, Armband, BodyArmor, Rig, Primary, Secondary, Holster, Sheath,
        Backpack, SecureContainer,
    }

    /// <summary>Static data of an item type. Plain data: the catalogue lives in <see cref="ItemDatabase"/>.</summary>
    public sealed class ItemDef
    {
        public string Id;
        public string Name;
        public string ShortName;
        public string Description = "";
        public ItemCategory Category;
        public int Width = 1, Height = 1;
        public int MaxStack = 1;
        public float WeightKg;
        public int Price;
        /// <summary>Inner grids of a container item (rig, backpack, case), as width x height pairs.</summary>
        public GridShape[] Grids = System.Array.Empty<GridShape>();
        /// <summary>Weapon part id understood by the armorer (<c>AttachmentRules.Id</c>), for weapon parts.</summary>
        public string AttachmentId;
        /// <summary>Only for weapons: the weapon family (armorer / match weapon).</summary>
        public string WeaponId;
        /// <summary>Weapons are two-handed long guns (Primary/Secondary) or handguns (Holster).</summary>
        public bool LongGun;
        /// <summary>Free-form "key: value" lines shown when inspecting (calibre, damage, heal...).</summary>
        public string[] Properties = System.Array.Empty<string>();

        public bool Stackable => MaxStack > 1;
        public bool IsContainer => Grids.Length > 0;

        /// <summary>Whether this item may go into <paramref name="slot"/>.</summary>
        public bool Fits(EquipSlot slot)
        {
            switch (slot)
            {
                case EquipSlot.Earpiece: return Category == ItemCategory.Earpiece;
                case EquipSlot.Headwear: return Category == ItemCategory.Headwear;
                case EquipSlot.FaceCover: return Category == ItemCategory.FaceCover;
                case EquipSlot.Eyewear: return Category == ItemCategory.Eyewear;
                case EquipSlot.Armband: return Category == ItemCategory.Armband;
                case EquipSlot.BodyArmor: return Category == ItemCategory.BodyArmor;
                case EquipSlot.Rig: return Category == ItemCategory.Rig;
                case EquipSlot.Primary:
                case EquipSlot.Secondary: return Category == ItemCategory.Weapon && LongGun;
                case EquipSlot.Holster: return Category == ItemCategory.Weapon && !LongGun;
                case EquipSlot.Sheath: return Category == ItemCategory.Melee;
                case EquipSlot.Backpack: return Category == ItemCategory.Backpack;
                default: return Category == ItemCategory.SecureContainer;
            }
        }
    }

    public struct GridShape
    {
        public int Width, Height;
        public GridShape(int width, int height) { Width = width; Height = height; }
    }

    /// <summary>Item types by id.</summary>
    public sealed class ItemDatabase
    {
        private readonly Dictionary<string, ItemDef> _byId = new Dictionary<string, ItemDef>();
        private readonly List<ItemDef> _all = new List<ItemDef>();

        public IReadOnlyList<ItemDef> All => _all;

        public void Add(ItemDef def)
        {
            _byId[def.Id] = def;
            _all.RemoveAll(d => d.Id == def.Id);
            _all.Add(def);
        }

        public ItemDef Get(string id) => id != null && _byId.TryGetValue(id, out ItemDef d) ? d : null;

        /// <summary>The item carrying the given armorer part id, or null.</summary>
        public ItemDef ForAttachment(string attachmentId)
        {
            if (string.IsNullOrEmpty(attachmentId)) return null;
            foreach (ItemDef d in _all)
                if (d.AttachmentId == attachmentId) return d;
            return null;
        }

        /// <summary>The Phase 1 catalogue: the M1911 and its parts plus the gear/loot a Tarkov-like stash needs.</summary>
        public static ItemDatabase Default()
        {
            var db = new ItemDatabase();
            void Add(string id, string name, string shortName, ItemCategory cat, int w, int h, float kg, int price,
                int stack = 1, string desc = "", params string[] props)
                => db.Add(new ItemDef
                {
                    Id = id, Name = name, ShortName = shortName, Category = cat, Width = w, Height = h, WeightKg = kg,
                    Price = price, MaxStack = stack, Description = desc, Properties = props,
                });

            db.Add(new ItemDef
            {
                Id = "m1911a1", Name = "Pistola Colt M1911A1 .45 ACP", ShortName = "M1911A1", Category = ItemCategory.Weapon,
                Width = 2, Height = 1, WeightKg = 1.1f, Price = 21500, WeaponId = "m1911",
                Description = "Pistola semiautomática de acción simple. Fiable, pesada y con una munición que para lo que toca.",
                Properties = new[] { "Calibre: .45 ACP", "Cadencia: semiautomática", "Cargador: 7 cartuchos" },
            });
            void Part(string id, string attachment, string name, string shortName, int w, int h, float kg, int price, string desc)
                => db.Add(new ItemDef
                {
                    Id = id, AttachmentId = attachment, Name = name, ShortName = shortName, Category = ItemCategory.WeaponPart,
                    Width = w, Height = h, WeightKg = kg, Price = price, Description = desc,
                });
            Part("barrel_standard", "barrel_standard", "Cañón M1911 estándar 127 mm", "Cañón", 2, 1, 0.12f, 3900, "Cañón de fábrica. No admite dispositivos de boca.");
            Part("barrel_threaded", "barrel_threaded", "Cañón M1911 roscado .578x28", "Cañón R", 2, 1, 0.14f, 9800, "Cañón con rosca para montar un silenciador.");
            Part("suppressor_45", "suppressor_45", "Silenciador .45 ACP", "Silenc.", 2, 1, 0.35f, 38000, "Reduce el ruido y el fogonazo. Requiere cañón roscado.");
            Part("grips_wood", "grips_wood", "Cachas de madera M1911", "Cachas", 1, 1, 0.05f, 2400, "Cachas de nogal con el escudo de fábrica.");
            db.Add(new ItemDef
            {
                Id = "magazine_7", AttachmentId = "magazine_7", Name = "Cargador M1911 de 7 cartuchos .45 ACP", ShortName = "Carg. 7",
                Category = ItemCategory.Magazine, WeightKg = 0.08f, Price = 2100, Description = "Cargador monohilera de acero.",
                Properties = new[] { "Capacidad: 7" },
            });

            Add("ammo_45_fmj", ".45 ACP FMJ", "FMJ", ItemCategory.Ammo, 1, 1, 0.015f, 180, 50,
                "Bala blindada estándar.", "Daño: 62", "Penetración: 19", "Velocidad: 253 m/s");
            Add("ammo_45_hp", ".45 ACP Hydra-Shok", "HS", ItemCategory.Ammo, 1, 1, 0.015f, 350, 50,
                "Punta hueca expansiva: mucho daño, poca penetración.", "Daño: 80", "Penetración: 9", "Velocidad: 244 m/s");

            Add("bandage", "Venda", "Venda", ItemCategory.Medical, 1, 1, 0.05f, 1600, 1, "Detiene hemorragias leves.", "Usos: 1", "Cura: hemorragia leve");
            Add("ai2", "Botiquín AI-2", "AI-2", ItemCategory.Medical, 1, 1, 0.1f, 3200, 1, "Botiquín individual: recupera algo de salud.", "Puntos de salud: 100");
            Add("carkit", "Botiquín de coche", "Coche", ItemCategory.Medical, 1, 2, 0.5f, 11000, 1, "Cura y detiene hemorragias leves.", "Puntos de salud: 220");
            Add("splint", "Férula inmovilizadora", "Férula", ItemCategory.Medical, 1, 1, 0.2f, 4500, 1, "Arregla fracturas.", "Usos: 1");
            Add("water", "Botella de agua 0,6 l", "Agua", ItemCategory.Food, 1, 2, 0.6f, 6800, 1, "Agua potable.", "Hidratación: +60");
            Add("tushonka", "Lata de carne estofada", "Carne", ItemCategory.Food, 1, 1, 0.35f, 9500, 1, "Tushonka de ternera.", "Energía: +60", "Hidratación: -10");
            Add("crackers", "Galletas del ejército", "Galletas", ItemCategory.Food, 1, 1, 0.1f, 4200, 1, "Secas pero llenan.", "Energía: +15", "Hidratación: -5");

            Add("bolts", "Tornillos", "Tornillos", ItemCategory.Barter, 1, 1, 0.2f, 9000, 1, "Material para intercambios.");
            Add("wires", "Manojo de cables", "Cables", ItemCategory.Barter, 1, 1, 0.1f, 11000, 1, "Material para intercambios.");
            Add("gunpowder", "Pólvora \"Águila\"", "Pólvora", ItemCategory.Barter, 2, 1, 0.5f, 26000, 1, "Material para intercambios.");
            Add("cigs", "Tabaco \"Estrella\"", "Tabaco", ItemCategory.Barter, 1, 1, 0.05f, 6500, 1, "Moneda de cambio en Polykov.");
            Add("key_dorm", "Llave de la residencia 214", "Res. 214", ItemCategory.Key, 1, 1, 0.01f, 15000, 1, "Abre una habitación de la residencia.");
            Add("roubles", "Rublos", "₽", ItemCategory.Money, 1, 1, 0f, 1, 500000, "Moneda local.");
            Add("knife", "Cuchillo de combate", "Cuchillo", ItemCategory.Melee, 1, 2, 0.3f, 7800, 1, "Arma cuerpo a cuerpo.");
            Add("cap", "Gorra táctica", "Gorra", ItemCategory.Headwear, 2, 2, 0.1f, 3500, 1, "No protege, pero queda bien.");
            Add("helmet", "Casco 6B47", "6B47", ItemCategory.Headwear, 2, 2, 1.3f, 42000, 1, "Casco de aramida.", "Clase de protección: 3", "Zonas: parte superior, nuca");
            Add("headset", "Auriculares tácticos", "Cascos", ItemCategory.Earpiece, 2, 2, 0.4f, 29000, 1, "Amplifican los pasos.");
            Add("glasses", "Gafas balísticas", "Gafas", ItemCategory.Eyewear, 2, 1, 0.05f, 9000, 1, "Protegen de la cegera por destellos.");
            Add("balaclava", "Pasamontañas", "Pasam.", ItemCategory.FaceCover, 2, 2, 0.1f, 2800, 1, "Anonimato.");
            Add("armband", "Brazalete azul", "Azul", ItemCategory.Armband, 1, 1, 0.05f, 500, 1, "Para distinguir a los tuyos.");
            Add("armor", "Chaleco balístico PACA", "PACA", ItemCategory.BodyArmor, 3, 3, 3.5f, 48000, 1, "Chaleco blando.", "Clase de protección: 2", "Zonas: tórax, abdomen");

            db.Add(new ItemDef
            {
                Id = "rig", Name = "Chaleco táctico de pecho", ShortName = "Chaleco", Category = ItemCategory.Rig, Width = 2, Height = 3,
                WeightKg = 0.9f, Price = 26000, Description = "Portacargadores con seis bolsillos.",
                Grids = new[] { new GridShape(1, 2), new GridShape(1, 2), new GridShape(1, 2), new GridShape(1, 2), new GridShape(1, 1), new GridShape(1, 1) },
            });
            db.Add(new ItemDef
            {
                Id = "backpack", Name = "Mochila de asalto", ShortName = "Mochila", Category = ItemCategory.Backpack, Width = 4, Height = 5,
                WeightKg = 1.2f, Price = 31000, Description = "Mochila de 25 litros.", Grids = new[] { new GridShape(5, 5) },
            });
            db.Add(new ItemDef
            {
                Id = "sling", Name = "Bolsa de transporte", ShortName = "Bolsa", Category = ItemCategory.Backpack, Width = 2, Height = 2,
                WeightKg = 0.3f, Price = 5000, Description = "Bolsa pequeña de tela.", Grids = new[] { new GridShape(2, 3) },
            });
            db.Add(new ItemDef
            {
                Id = "alpha", Name = "Contenedor seguro Alfa", ShortName = "Alfa", Category = ItemCategory.SecureContainer, Width = 2, Height = 2,
                WeightKg = 0.8f, Price = 0, Description = "Lo que guardes aquí no se pierde al morir.", Grids = new[] { new GridShape(2, 2) },
            });
            db.Add(new ItemDef
            {
                Id = "ammo_case", Name = "Caja de munición", ShortName = "Caja M", Category = ItemCategory.Container, Width = 2, Height = 2,
                WeightKg = 1f, Price = 60000, Description = "Guarda munición y cargadores.", Grids = new[] { new GridShape(4, 4) },
            });
            return db;
        }
    }
}
