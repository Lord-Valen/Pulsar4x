{
  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixpkgs-unstable";

    devshell.url = "github:numtide/devshell";
    nixago.url = "github:nix-community/nixago";
    nixago.inputs.nixpkgs.follows = "nixpkgs";
    std.url = "github:divnix/std";
    std.inputs = {
      devshell.follows = "devshell";
      nixago.follows = "nixago";
    };
  };
  outputs =
    { std, self, ... }@inputs:
    std.growOn
      {
        inherit inputs;
        cellsFrom = ./nix;
        cellBlocks = with std.blockTypes; [
          (devshells "devshells")
          (nixago "configs")
          (installables "packages")
        ];
      }
      {
        packages = std.harvest self [
          "pulsar"
          "packages"
        ];
        devShells = std.harvest self [
          "repo"
          "devshells"
        ];
      };
}
