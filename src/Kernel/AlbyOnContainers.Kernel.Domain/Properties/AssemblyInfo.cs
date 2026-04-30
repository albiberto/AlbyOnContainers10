using System.Runtime.CompilerServices;

// Persistence-layer interceptors need access to internal audit/event-clearing methods.
[assembly: InternalsVisibleTo("AlbyOnContainers.Kernel.Persistence")]

