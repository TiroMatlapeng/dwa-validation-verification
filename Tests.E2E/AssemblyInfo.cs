// The whole E2E suite runs against ONE shared Kestrel host and ONE shared
// dwa_val_ver_e2e database (see KestrelAppFixture). Serialization is what keeps
// the shared DB, the bound port, and the HHmmssfff unique-suffix scheme safe.
// Every test class already sits in the single E2ECollection, but that invariant
// is load-bearing and easy to break by adding a class with its own collection —
// so parallelization is disabled assembly-wide, explicitly.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
