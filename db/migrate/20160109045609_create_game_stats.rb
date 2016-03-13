class CreateGameStats < ActiveRecord::Migration
  def change
    create_table :game_stats do |t|
      t.references :team,   index: true
      t.references :opponent, index: true
      t.boolean :home
      t.integer :decision
      t.date :date

      t.integer :goals
      t.integer :ppg
      t.integer :shg
      t.integer :evg
      t.integer :shots
      t.integer :pim
    end

    remove_column :player_stats, :team_id
    remove_column :player_stats, :opponent_id
    remove_column :player_stats, :home
    remove_column :player_stats, :decision
    remove_column :player_stats, :date

    add_column :player_stats, :game_id, :integer
    add_index :player_stats, [:game_id]

    remove_column :goalie_stats, :team_id
    remove_column :goalie_stats, :opponent_id
    remove_column :goalie_stats, :home
    remove_column :goalie_stats, :decision
    remove_column :goalie_stats, :date

    add_column :goalie_stats, :game_id, :integer
    add_index :goalie_stats, [:game_id]
  end
end
