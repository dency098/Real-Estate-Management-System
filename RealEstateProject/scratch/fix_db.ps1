$connectionString = "Server=localhost;Database=RealEstateProject;Trusted_Connection=True;Encrypt=False"
$connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
$connection.Open()
$command = $connection.CreateCommand()
$command.CommandText = "ALTER TABLE Notifications ALTER COLUMN Message NVARCHAR(500) NOT NULL"
$rowsAffected = $command.ExecuteNonQuery()
Write-Output "Successfully altered Notifications table column Message to NVARCHAR(500)!"
$connection.Close()
