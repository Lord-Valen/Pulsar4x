{
  buildDotnetModule,
  stdInputs,
  dotnetCorePackages,
  SDL2,
  lib,
}:
buildDotnetModule {
  pname = "pulsar4x";
  version = "0-unstable";

  src = stdInputs.std.incl (stdInputs.self) [
    "LICENSE"
    "Pulsar4X"
  ];
  projectFile = "Pulsar4X/Pulsar4X.sln";
  nugetDeps = ./deps.nix;

  dotnet-sdk = dotnetCorePackages.sdk_7_0;
  dotnet-runtime = dotnetCorePackages.runtime_7_0;

  runtimeDeps = [ SDL2 ];

  executables = [ "Pulsar4X.Client" ];

  meta = {
    license = with lib.licenses; [ mit ];
    maintainers = with lib.maintainers; [ lord-valen ];
    mainProgram = "Pulsar4X.Client";
  };
}
