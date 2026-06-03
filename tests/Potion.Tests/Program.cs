static class Program
{
    public static int Main()
    {
        var runner = new TestRunner();

        PotionBrewingServiceTests.Register(runner);
        InventoryAndBrewPanelTests.Register(runner);
        RecipeAndPotionBookTests.Register(runner);
        CustomerFlowTests.Register(runner);
        RuntimeContentAndDataDbTests.Register(runner);
        PersistenceTests.Register(runner);
        GameStateTests.Register(runner);
        GardenTests.Register(runner);
        TutorialTests.Register(runner);
        SceneAndHudWiringTests.Register(runner);

        return runner.Finish();
    }
}
