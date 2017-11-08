namespace Server.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddHero : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.HeroBuffs",
                c => new
                    {
                        Index = c.Int(nullable: false, identity: true),
                        Type = c.Byte(nullable: false),
                        CasterName = c.String(),
                        Visible = c.Boolean(nullable: false),
                        DBObjectID = c.Long(nullable: false),
                        ExpireTime = c.Long(nullable: false),
                        DbValues = c.String(),
                        Infinite = c.Boolean(nullable: false),
                        RealTime = c.Boolean(nullable: false),
                        RealTimeExpire = c.DateTime(precision: 7, storeType: "datetime2"),
                        Paused = c.Boolean(nullable: false),
                        HeroIndex = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Index)
                .ForeignKey("dbo.HeroInfoes", t => t.HeroIndex, cascadeDelete: true)
                .Index(t => t.HeroIndex);
            
            CreateTable(
                "dbo.HeroInfoes",
                c => new
                    {
                        Index = c.Int(nullable: false, identity: true),
                        Name = c.String(),
                        DBLevel = c.Int(nullable: false),
                        Class = c.Byte(nullable: false),
                        Gender = c.Byte(nullable: false),
                        Hair = c.Byte(nullable: false),
                        CurrentMapIndex = c.Int(nullable: false),
                        DBCurrentLocation = c.String(),
                        Direction = c.Byte(nullable: false),
                        DBHP = c.Int(nullable: false),
                        DBMP = c.Int(nullable: false),
                        Experience = c.Long(nullable: false),
                        AMode = c.Byte(nullable: false),
                        Thrusting = c.Boolean(nullable: false),
                        HalfMoon = c.Boolean(nullable: false),
                        CrossHalfMoon = c.Boolean(nullable: false),
                        DoubleSlash = c.Boolean(nullable: false),
                        MentalState = c.Byte(nullable: false),
                        MentalStateLvl = c.Byte(nullable: false),
                        Grade = c.Byte(nullable: false),
                        NeedUnlock = c.Boolean(nullable: false),
                        HeroMode = c.Byte(nullable: false),
                        PearlCount = c.Int(nullable: false),
                        CharacterIndex = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Index)
                .ForeignKey("dbo.CharacterInfoes", t => t.CharacterIndex, cascadeDelete: true)
                .Index(t => t.CharacterIndex);
            
            CreateTable(
                "dbo.HeroEquipmentItems",
                c => new
                    {
                        id = c.Long(nullable: false, identity: true),
                        HeroIndex = c.Int(nullable: false),
                        ItemUniqueID = c.Long(),
                    })
                .PrimaryKey(t => t.id)
                .ForeignKey("dbo.HeroInfoes", t => t.HeroIndex, cascadeDelete: true)
                .ForeignKey("dbo.UserItems", t => t.ItemUniqueID)
                .Index(t => t.HeroIndex)
                .Index(t => t.ItemUniqueID);
            
            CreateTable(
                "dbo.HeroInventoryItems",
                c => new
                    {
                        id = c.Long(nullable: false, identity: true),
                        HeroIndex = c.Int(nullable: false),
                        ItemUniqueID = c.Long(),
                    })
                .PrimaryKey(t => t.id)
                .ForeignKey("dbo.HeroInfoes", t => t.HeroIndex, cascadeDelete: true)
                .ForeignKey("dbo.UserItems", t => t.ItemUniqueID)
                .Index(t => t.HeroIndex)
                .Index(t => t.ItemUniqueID);
            
            CreateTable(
                "dbo.HeroMagics",
                c => new
                    {
                        id = c.Long(nullable: false, identity: true),
                        Spell = c.Byte(nullable: false),
                        HeroIndex = c.Int(nullable: false),
                        Level = c.Byte(nullable: false),
                        Key = c.Byte(nullable: false),
                        DBExperience = c.Int(nullable: false),
                        IsTempSpell = c.Boolean(nullable: false),
                        CastTime = c.Long(nullable: false),
                    })
                .PrimaryKey(t => t.id)
                .ForeignKey("dbo.HeroInfoes", t => t.HeroIndex, cascadeDelete: true)
                .Index(t => t.HeroIndex);
            
            AddColumn("dbo.CharacterInfoes", "MaxHeroCount", c => c.Byte(nullable: false));
            AddColumn("dbo.CharacterInfoes", "CurrentHeroIndex", c => c.Int(nullable: false));
            AddColumn("dbo.MapInfoes", "NoHero", c => c.Boolean(nullable: false));
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.HeroMagics", "HeroIndex", "dbo.HeroInfoes");
            DropForeignKey("dbo.HeroInventoryItems", "ItemUniqueID", "dbo.UserItems");
            DropForeignKey("dbo.HeroInventoryItems", "HeroIndex", "dbo.HeroInfoes");
            DropForeignKey("dbo.HeroEquipmentItems", "ItemUniqueID", "dbo.UserItems");
            DropForeignKey("dbo.HeroEquipmentItems", "HeroIndex", "dbo.HeroInfoes");
            DropForeignKey("dbo.HeroBuffs", "HeroIndex", "dbo.HeroInfoes");
            DropForeignKey("dbo.HeroInfoes", "CharacterIndex", "dbo.CharacterInfoes");
            DropIndex("dbo.HeroMagics", new[] { "HeroIndex" });
            DropIndex("dbo.HeroInventoryItems", new[] { "ItemUniqueID" });
            DropIndex("dbo.HeroInventoryItems", new[] { "HeroIndex" });
            DropIndex("dbo.HeroEquipmentItems", new[] { "ItemUniqueID" });
            DropIndex("dbo.HeroEquipmentItems", new[] { "HeroIndex" });
            DropIndex("dbo.HeroInfoes", new[] { "CharacterIndex" });
            DropIndex("dbo.HeroBuffs", new[] { "HeroIndex" });
            DropColumn("dbo.MapInfoes", "NoHero");
            DropColumn("dbo.CharacterInfoes", "CurrentHeroIndex");
            DropColumn("dbo.CharacterInfoes", "MaxHeroCount");
            DropTable("dbo.HeroMagics");
            DropTable("dbo.HeroInventoryItems");
            DropTable("dbo.HeroEquipmentItems");
            DropTable("dbo.HeroInfoes");
            DropTable("dbo.HeroBuffs");
        }
    }
}
