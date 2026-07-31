// UnitsCollection.Units is process-wide state that the roster tests swap out and restore,
// so test classes must not run against it concurrently.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
