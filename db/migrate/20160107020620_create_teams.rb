class CreateTeams < ActiveRecord::Migration
    def change
        create_table :teams do |t|
            t.string :name
            t.string :abbreviation
            t.string :city
            t.string :state
            t.string :full_name
        end
    end
end
