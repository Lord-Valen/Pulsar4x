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
      { package = pkgs.nuget-to-nix; }
      {
        name = "update-pulsar-deps";
        command = ''
          tmp="$PRJ_ROOT"/tmp

          dotnet restore "$PRJ_ROOT"/Pulsar4X/Pulsar4X.sln --packages $tmp \
          && nuget-to-nix $tmp > "$PRJ_ROOT"/nix/pulsar/packages/pulsar4x/deps.nix
          [ -d "$tmp" ] && rm -r "$PRJ_ROOT"/tmp
        '';
      }
    ];
  };
}
