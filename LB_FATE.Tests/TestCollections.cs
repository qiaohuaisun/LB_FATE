using Xunit;

[CollectionDefinition("LoggerSeq", DisableParallelization = true)]
public class LoggerSeqCollection : ICollectionFixture<object> { }

