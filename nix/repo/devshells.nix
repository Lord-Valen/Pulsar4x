{ inputs, cell }:
let
  pkgs = inputs.nixpkgs;
  lib = pkgs.lib;
  inherit (inputs.std.lib) dev cfg;
in
{
  default = dev.mkShell {
    name = "Pulsar4x";
    nixago =
      map
        (
          name:
          if lib.hasAttr name cfg then
            dev.mkNixago cfg."${name}" cell.configs."${name}"
          else
            dev.mkNixago cell.configs."${name}"
        )
        [
          "treefmt"
          "lefthook"
        ];
    packages = with pkgs; [ SDL2 ];
    commands = [
      {
        name = "dotnet";
        package = pkgs.dotnetCorePackages.sdk_7_0;
      }
    ];
  };
}
