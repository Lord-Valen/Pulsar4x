{ inputs, cell }:
let
  lib = inputs.nixpkgs.lib;
  dirs = lib.filterAttrs (_: type: type == "directory") (builtins.readDir ./.);
  packages = lib.mapAttrs (
    name: _:
    lib.callPackageWith (inputs.nixpkgs // { stdInputs = inputs; }) (./. + "/${name}/package.nix") { }
  ) dirs;
in
packages // { default = packages.pulsar4x; }
