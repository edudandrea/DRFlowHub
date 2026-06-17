ALTER TABLE "User" ADD "Ativo" INTEGER NOT NULL DEFAULT 1;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260509124649_AddUserAtivo', '10.0.7');
