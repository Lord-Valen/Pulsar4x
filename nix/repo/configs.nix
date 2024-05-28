{ inputs, cell }:
let
  pkgs = inputs.nixpkgs;
in
{
  conform = {
    data = {
      inherit (inputs) cells;
      commit = {
        header = {
          length = 89;
        };
        conventional = {
          types = [
            "build"
            "chore"
            "ci"
            "docs"
            "feat"
            "fix"
            "perf"
            "refactor"
            "style"
            "test"
          ];
          scopes = [ ];
        };
      };
    };
  };

  treefmt = {
    packages = with pkgs; [
      nixfmt-rfc-style
      dotnetCorePackages.sdk_7_0
      nodePackages.prettier
    ];
    data = {
      excludes = [
        "malformedData/*"
        "deps.nix"
      ];
      formatter = {
        nix = {
          command = "nixfmt";
          options = [ ];
          includes = [ "*.nix" ];
          excludes = [ "deps.nix" ];
        };
        dotnet = {
          command = "dotnet";
          options = [
            "format"
            "Pulsar4X/Pulsar4X.sln"
            "--include"
          ];
          includes = [ "*.cs" ];
        };
        prettier = {
          command = "prettier";
          options = [ "--write" ];
          includes = [
            "*.json"
            "*.md"
            "*.yaml"
          ];
        };
      };
    };
  };

  lefthook = {
    data = {
      pre-commit.commands = {
        treefmt = {
          run = "treefmt --fail-on-change {staged_files}";
          skip = [
            "merge"
            "rebase"
          ];
        };
      };
    };
  };
}
