using SpacetimeDB;

namespace BioChain.Module;

[SpacetimeDB.Type]
public enum ActivationMode : byte { Threshold, Rate, Burst, Tonic }
