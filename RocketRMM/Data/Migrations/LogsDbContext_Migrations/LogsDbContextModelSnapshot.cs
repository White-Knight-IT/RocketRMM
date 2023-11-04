﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RocketRMM.Data.Logging;

#nullable disable

namespace RocketRMM.Data.Migrations.LogsDbContext_Migrations
{
    [DbContext(typeof(LogsDbContext))]
    partial class LogsDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.0-rc.1.23419.6")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            modelBuilder.Entity("RocketRMM.Data.Logging.LogEntry", b =>
                {
                    b.Property<Guid>("RowKey")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("char(36)");

                    b.Property<string>("API")
                        .HasColumnType("longtext");

                    b.Property<string>("Message")
                        .HasColumnType("longtext");

                    b.Property<bool?>("SentAsAlert")
                        .HasColumnType("tinyint(1)");

                    b.Property<string>("Severity")
                        .HasColumnType("longtext");

                    b.Property<DateTime>("Timestamp")
                        .HasColumnType("datetime(6)");

                    b.Property<string>("Username")
                        .HasColumnType("longtext");

                    b.HasKey("RowKey");

                    b.ToTable("_logEntries");
                });
#pragma warning restore 612, 618
        }
    }
}
