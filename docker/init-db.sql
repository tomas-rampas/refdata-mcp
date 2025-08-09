-- Create databases for Jira and Confluence
CREATE DATABASE jiradb;
CREATE DATABASE confluencedb;

-- Grant permissions to the user
GRANT ALL PRIVILEGES ON DATABASE jiradb TO myuser;
GRANT ALL PRIVILEGES ON DATABASE confluencedb TO myuser;