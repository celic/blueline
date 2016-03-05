class MoveTeamFromPlayerToPlayerStat < ActiveRecord::Migration
  def change
  	remove_column :players, :team_id
  	add_column :player_stats, :team_id, :integer
  end
end
