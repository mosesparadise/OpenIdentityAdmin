// The isolation scenario explicitly overlaps two environments. Keep the surrounding
// matrix sequential to bound container memory and avoid implicit test-runner overlap.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
