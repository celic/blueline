class CreateGames < ActiveRecord::Migration
  def change
    create_table :games do |t|
      t.references :home,   index: true
      t.references :away,   index: true
      t.date :date
    end
  end

  add_column :game_stats, :game_id, :integer
  add_index :game_stats, [:game_id]

  remove_column :game_stats, :opponent_id
  remove_column :game_stats, :date
end
