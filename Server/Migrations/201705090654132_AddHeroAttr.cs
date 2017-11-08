namespace Server.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddHeroAttr : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.HeroInfoes", "HeroOrbId", c => c.Long(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.HeroInfoes", "HeroOrbId");
        }
    }
}
