class CreatePlayerStats < ActiveRecord::Migration
    def change
        create_table :player_stats do |t|
            t.references :player, index: true
            t.references :team, index: true
            t.references :opponent
            t.boolean :home
            t.integer :decision

            t.integer :goals
            t.integer :assists
            t.integer :points

            t.integer :plusminus
            t.integer :pim

            t.integer :evg
            t.integer :ppg
            t.integer :shg
            t.integer :gwg

            t.integer :eva
            t.integer :ppa
            t.integer :sha

            t.integer :shots
            t.decimal :shot_percentage, precision: 5, scale: 2

            t.integer :shifts
            t.integer :toi
        end
    end
end
