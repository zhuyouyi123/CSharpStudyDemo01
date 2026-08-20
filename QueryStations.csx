using System;
using System.Data;
using Microsoft.Data.SqlClient;

var connStr = "Server=192.168.190.100,1433;Database=master;User Id=sa;Password=1234567;TrustServerCertificate=True;Connection Timeout=5";
using var conn = new SqlConnection(connStr);
conn.Open();
using var cmd = new SqlCommand("SELECT station, x, y, frontsite, backsite, finalsite, groups FROM agvstationinfo ORDER BY station", conn);
using var reader = cmd.ExecuteReader();
while (reader.Read())
{
    var station = reader[0];
    var x = reader[1];
    var y = reader[2];
    var front = reader[3];
    var back = reader[4];
    var finalSite = reader[5];
    var groups = reader[6];
    Console.WriteLine($"station={station}\tx={x}\ty={y}\tfrontsite={front}\tbacksite={back}\tfinalsite={finalSite}\tgroups={groups}");
}
